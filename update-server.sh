#!/usr/bin/env bash
# MQ Rewritten: updates a Linux server to the latest code and restarts it.
#
#   bash MQRewritten/update-server.sh            pull the latest code, rebuild, restart
#   bash MQRewritten/update-server.sh --restart  no code update: restart so new files in Game/Win are picked up
#
# This repo must sit beside docker-compose.yml, .env, Build and Game. The first start unpacks 12 GB of bundles, so it
# can take a long time; later runs skip that.
#
# Everything is inside main so that git pull can safely replace this file while it's running.

main() {
    set -euo pipefail

    local repo root container download
    repo="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
    root="$(dirname "$repo")"
    container="mqreawakened"
    download="/data/Downloads/Play MQ Rewritten.zip"

    cd "$root"

    if [[ ! -f docker-compose.yml ]]; then
        echo "No docker-compose.yml in $root" >&2
        exit 1
    fi

    if [[ "${1:-}" == "--restart" ]]; then
        echo "Restarting..."
        docker compose restart
    else
        echo "Pulling the latest code..."
        git -C "$repo" pull --ff-only
        echo "Rebuilding..."
        docker compose up -d --build
    fi

    echo "Waiting for the server (the first start can take a long time)..."
    local waited=0 status running ready
    while true; do
        running="$(docker inspect -f '{{.State.Running}}' "$container" 2>/dev/null || echo false)"
        if [[ "$running" != "true" ]]; then
            echo "The container isn't running. Check: docker logs --tail 50 $container" >&2
            exit 1
        fi

        status="$(docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{end}}' "$container")"
        # grep -c reads to the end: grep -q stops at the first match, which fails the pipe under pipefail on a long log.
        ready="$(docker logs "$container" 2>&1 | grep -cE 'Game archive created|Download zip already exists' || true)"
        if [[ "$status" == "healthy" && "$ready" -gt 0 ]]; then
            break
        fi

        if (( waited >= 5400 )); then
            echo "Gave up after 90 minutes. Check: docker logs --tail 50 $container" >&2
            exit 1
        fi

        sleep 10
        waited=$((waited + 10))
    done

    # A rebuild swaps the website download for the old Unity launcher, so put the Godot client back. This runs inside
    # the container because the server's files belong to root.
    if docker exec "$container" test -f /data/Game/Win/MQClient.zip; then
        docker exec "$container" cp /data/Game/Win/MQClient.zip "$download"
        echo "Website download restored."
    else
        echo "Warning: Game/Data/Game/Win/MQClient.zip is missing, so the website download wasn't restored." >&2
    fi

    echo "Done."
}

main "$@"
exit
