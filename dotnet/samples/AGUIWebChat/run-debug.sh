#!/bin/bash
set -e

# Script to run AGUIWebChat Server and Client for debugging
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVER_PROJECT="$SCRIPT_DIR/Server/AGUIWebChatServer.csproj"
CLIENT_PROJECT="$SCRIPT_DIR/Client/AGUIWebChatClient.csproj"

# Variables for PIDs
SERVER_PID=""
CLIENT_PID=""

# Cleanup function
cleanup() {
    echo ""
    echo "Stopping background processes..."
    if [ -n "$SERVER_PID" ]; then
        echo "Killing Server (PID: $SERVER_PID)"
        kill "$SERVER_PID" 2>/dev/null || true
    fi
    if [ -n "$CLIENT_PID" ]; then
        echo "Killing Client (PID: $CLIENT_PID)"
        kill "$CLIENT_PID" 2>/dev/null || true
    fi
}

# Trap exit to ensure cleanup
trap cleanup EXIT

echo "Starting AGUIWebChat Server on http://localhost:6100..."
dotnet run --project "$SERVER_PROJECT" --urls=http://localhost:6100 2>&1 | sed $'s/^/\033[31m[SERVER]\033[0m /' &
SERVER_PID=$!
echo "Server PID: $SERVER_PID"

echo "Starting AGUIWebChat Client on http://localhost:7000..."
dotnet run --project "$CLIENT_PROJECT" --urls=http://localhost:7000 2>&1 | sed $'s/^/\033[32m[CLIENT]\033[0m /' &
CLIENT_PID=$!
echo "Client PID: $CLIENT_PID"

echo "Both services are running. Press Ctrl+C to stop."
echo "Client: http://localhost:7000"
echo "Server: http://localhost:6100"

# Wait for processes
wait