#!/bin/bash
set -e

# Script to run AGUIDojo Server and Client for debugging
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVER_PROJECT="$SCRIPT_DIR/AGUIDojoServer/AGUIDojoServer.csproj"
CLIENT_PROJECT="$SCRIPT_DIR/AGUIDojoClient/AGUIDojoClient.csproj"

# Variables for process identifiers
SERVER_CMD="dotnet run --project.*AGUIDojoServer.csproj"
CLIENT_CMD="dotnet run --project.*AGUIDojoClient.csproj"

# Cleanup function
cleanup() {
    echo ""
    echo "Stopping background processes..."
    echo "Killing Server processes..."
    pkill -f "$SERVER_CMD" 2>/dev/null || true
    echo "Killing Client processes..."
    pkill -f "$CLIENT_CMD" 2>/dev/null || true
}

# Trap exit and signals to ensure cleanup
trap cleanup EXIT SIGINT SIGTERM

echo "Starting AGUIDojoServer on http://localhost:5100..."
dotnet run --project "$SERVER_PROJECT" --framework net10.0 --urls=http://localhost:5100 2>&1 | sed $'s/^/\033[31m[SERVER]\033[0m /' &

echo "Starting AGUIDojoClient on http://localhost:6001..."
SERVER_URL=http://localhost:5100 dotnet run --project "$CLIENT_PROJECT" --framework net10.0 --urls=http://localhost:6001 2>&1 | sed $'s/^/\033[32m[CLIENT]\033[0m /' &

echo "Both services are running. Press Ctrl+C to stop."
echo "Client: http://localhost:6001"
echo "Server: http://localhost:5100"

# Wait for processes
wait
