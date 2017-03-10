#!/bin/bash

# Copyright (c) Winton. All rights reserved.
# Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

set -euo pipefail;

VERSION_AND_PUBLISH="false";

while getopts v option;
do
    case "${option}"
    in
            v) VERSION_AND_PUBLISH="true";;
    esac
done;

echo "Cleaning ..."
dotnet clean
echo

echo "Restoring ..."
dotnet restore
echo

if [[ "$VERSION_AND_PUBLISH" == "true" ]]; then
    echo "Versioning ..."
    (cd etc/scripts && ./write_version_targets.sh)
    echo
else
    echo "WARN: Skipping versioning."
    echo
fi

echo "Building ..."
dotnet build --configuration Release
echo

echo "Testing ..."
(cd Winton.Extensions.Threading.Actor.Tests.Unit && dotnet test --no-build --configuration Release)
echo

if [[ "$VERSION_AND_PUBLISH" == "true" ]]; then
    echo "Packing ..."
    (cd Winton.Extensions.Threading.Actor && dotnet pack --no-build --configuration Release)
fi
