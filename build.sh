#!/bin/bash

# Copyright (c) Winton. All rights reserved.
# Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

set -euo pipefail;

VERSION_AND_PUBLISH="true";

if [ "${TRAVIS_PULL_REQUEST:-}" == "true" ]; then
    echo "WARN: Skipping versioning."
    echo
else
    echo "Versioning ..."
    ./etc/scripts/write_version_targets.sh
    echo
fi

echo "Cleaning ..."
dotnet clean
echo

echo "Building ..."
dotnet build ./Winton.Extensions.Threading.Actor/Winton.Extensions.Threading.Actor.csproj --configuration Release --framework netstandard1.3
dotnet build ./Winton.Extensions.Threading.Actor/Winton.Extensions.Threading.Actor.csproj --configuration Release --framework netstandard2.0
echo

echo "Testing ..."
dotnet test ./Winton.Extensions.Threading.Actor.Tests.Unit/Winton.Extensions.Threading.Actor.Tests.Unit.csproj --configuration Release --framework netcoreapp2.0
dotnet test ./Winton.Extensions.Threading.Actor.Tests.Unit/Winton.Extensions.Threading.Actor.Tests.Unit.csproj --configuration Release --framework netcoreapp1.1
echo

if [ "${TRAVIS:-}" != "true" ]; then
    echo "Packing ..."
    dotnet pack ./Winton.Extensions.Threading.Actor/Winton.Extensions.Threading.Actor.csproj --no-build --no-restore --configuration Release
fi
