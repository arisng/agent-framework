#!/bin/bash
set -e

# Script to run AGUIWebChat Server and Client for debugging
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVER_PROJECT="$SCRIPT_DIR/Server/AGUIWebChatServer.csproj"
CLIENT_PROJECT="$SCRIPT_DIR/Client/AGUIWebChatClient.csproj"

# Variables for process identifiers
SERVER_CMD="dotnet run --project.*AGUIWebChatServer.csproj"
CLIENT_CMD="dotnet run --project.*AGUIWebChatClient.csproj"

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

echo "Starting AGUIWebChat Server on http://localhost:6100..."
dotnet run --project "$SERVER_PROJECT" --urls=http://localhost:6100 2>&1 | sed $'s/^/\033[31m[SERVER]\033[0m /' &

echo "Starting AGUIWebChat Client on http://localhost:7000..."
dotnet run --project "$CLIENT_PROJECT" --urls=http://localhost:7000 2>&1 | sed $'s/^/\033[32m[CLIENT]\033[0m /' &

echo "Both services are running. Press Ctrl+C to stop."
echo "Client: http://localhost:7000"
echo "Server: http://localhost:6100"

# Wait for processes
wait