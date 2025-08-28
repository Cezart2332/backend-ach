#!/usr/bin/env bash
set -euo pipefail

# recover_uploads.sh
# Usage:
#  sudo ./recover_uploads.sh --container old-container-name
#  sudo ./recover_uploads.sh --image registry/name:tag [--uid 1000 --gid 1000]
#  sudo ./recover_uploads.sh --volume uploads-volume-name [--uid 1000 --gid 1000]
#
# This script copies /var/www/uploads from the given source into the host's
# /var/www/uploads directory, applies ownership based on the container's
# runtime UID/GID (if available), and leaves the host dir ready for a
# bind-mount into the new container.

HOST_UPLOADS_DIR="/var/www/uploads"
TMP_BACKUP_DIR="/tmp/uploads_backup_$$"

print_usage() {
  cat <<EOF
Usage:
  $0 --container <container-name>
  $0 --image <image-name:tag> [--uid <uid> --gid <gid>]
  $0 --volume <volume-name> [--uid <uid> --gid <gid>]

Examples:
  sudo $0 --container old-app
  sudo $0 --image myregistry/myapp:latest --uid 1000 --gid 1000
  sudo $0 --volume uploads-data
EOF
}

if [ "$#" -lt 2 ]; then
  print_usage
  exit 1
fi

MODE=""
ARG=""
FORCE_UID=""
FORCE_GID=""

while [ "$#" -gt 0 ]; do
  case "$1" in
    --container) MODE="container"; ARG="$2"; shift 2 ;;
    --image)     MODE="image";     ARG="$2"; shift 2 ;;
    --volume)    MODE="volume";    ARG="$2"; shift 2 ;;
    --uid)       FORCE_UID="$2"; shift 2 ;;
    --gid)       FORCE_GID="$2"; shift 2 ;;
    -h|--help)   print_usage; exit 0 ;;
    *) echo "Unknown arg: $1"; print_usage; exit 1 ;;
  esac
done

echo "Preparing host folder: $HOST_UPLOADS_DIR"
mkdir -p "$HOST_UPLOADS_DIR"
chmod 0775 "$HOST_UPLOADS_DIR"

echo "Temporary backup directory: $TMP_BACKUP_DIR"
mkdir -p "$TMP_BACKUP_DIR"

if [ "$MODE" = "container" ]; then
  CONTAINER="$ARG"
  echo "Copying uploads from container: $CONTAINER"
  if ! docker ps -a --format '{{.Names}}' | grep -q -x "$CONTAINER"; then
    echo "Container '$CONTAINER' not found. Aborting."
    exit 2
  fi

  # Try to detect UID/GID used by the container's process
  CONTAINER_UID=""
  CONTAINER_GID=""
  if docker inspect "$CONTAINER" >/dev/null 2>&1; then
    # Start a short-lived sleep process to ensure we can exec; if container not running we create a temp
    if docker inspect -f '{{.State.Running}}' "$CONTAINER" | grep -q true; then
      CONTAINER_UID=$(docker exec "$CONTAINER" id -u 2>/dev/null || true)
      CONTAINER_GID=$(docker exec "$CONTAINER" id -g 2>/dev/null || true)
    else
      # create a temporary container from same image if possible
      IMAGE_NAME=$(docker inspect -f '{{.Config.Image}}' "$CONTAINER")
      TEMP_CONTAINER=$(docker create "$IMAGE_NAME" sh -c 'id -u || true') || true
      if [ -n "$TEMP_CONTAINER" ]; then
        CONTAINER_UID=$(docker start -ai "$TEMP_CONTAINER" >/dev/null 2>&1 || true)
        docker rm -f "$TEMP_CONTAINER" >/dev/null 2>&1 || true
      fi
    fi
  fi

  echo "Detected container UID/GID: $CONTAINER_UID / $CONTAINER_GID (may be empty)"
  docker cp "$CONTAINER":/var/www/uploads "$TMP_BACKUP_DIR" || true

elif [ "$MODE" = "image" ]; then
  IMAGE="$ARG"
  echo "Recovering uploads from image: $IMAGE"
  CID=$(docker create "$IMAGE")
  if [ -z "$CID" ]; then
    echo "Failed to create temporary container from image '$IMAGE'. Aborting."
    exit 3
  fi
  echo "Created temp container: $CID"
  docker cp "$CID":/var/www/uploads "$TMP_BACKUP_DIR" || true
  docker rm "$CID" >/dev/null || true

elif [ "$MODE" = "volume" ]; then
  VOLUME="$ARG"
  echo "Copying from volume: $VOLUME"
  docker run --rm -v "${VOLUME}":/data -v "${TMP_BACKUP_DIR}":/backup alpine \
    sh -c "cp -a /data/* /backup/ || true"

else
  echo "Unknown mode: $MODE"
  exit 1
fi

echo "Backup copy complete. Listing $TMP_BACKUP_DIR:"
ls -la "$TMP_BACKUP_DIR" || true

# Sync into host uploads dir
echo "Syncing into host uploads dir: $HOST_UPLOADS_DIR (preserves permissions)"
rsync -a --delete "$TMP_BACKUP_DIR"/ "$HOST_UPLOADS_DIR"/

# Determine ownership to apply
APPLY_UID=""
APPLY_GID=""
if [ -n "$FORCE_UID" ]; then
  APPLY_UID="$FORCE_UID"
  APPLY_GID="${FORCE_GID:-$FORCE_GID}"
fi

# If no forced UID/GID and we detected container UID/GID earlier (container mode), use them
if [ -z "$APPLY_UID" ] && [ "$MODE" = "container" ]; then
  APPLY_UID="$CONTAINER_UID"
  APPLY_GID="$CONTAINER_GID"
fi

# Fallback to 1000:1000
if [ -z "$APPLY_UID" ]; then
  APPLY_UID=1000
fi
if [ -z "$APPLY_GID" ]; then
  APPLY_GID=1000
fi

echo "Applying ownership: $APPLY_UID:$APPLY_GID to $HOST_UPLOADS_DIR"
chown -R "$APPLY_UID":"$APPLY_GID" "$HOST_UPLOADS_DIR" || true
chmod -R 0775 "$HOST_UPLOADS_DIR"

echo "Cleaning temporary backup"
rm -rf "$TMP_BACKUP_DIR"

echo "Done. Next steps:"
echo "  1) Ensure docker-compose.yml image is set to your image name (we updated the compose file with a placeholder)."
echo "  2) Deploy with: docker-compose pull && docker-compose up -d"
echo "  3) Verify example file via app URL"
echo "  4) Once verified remove the old container if desired: docker rm -f <old-container>"

exit 0
