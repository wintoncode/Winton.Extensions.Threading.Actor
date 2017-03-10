#!/bin/bash

# Copyright (c) Winton. All rights reserved.
# Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

set -euo pipefail

GIT=$(which git)

# Tries to determines a version similar to that output by GitVersion

# If a tag is checked out or we're bang on a tag anyway then getting the exact match
# will set branch to the version number. This will mean that below the branch is
# treated as a release no matter what the true name of the branch is.
branch="$($GIT describe --tags --exact-match --match='[0-9]*.[0-9]*.[0-9]*' 2>/dev/null || $GIT symbolic-ref -q --short HEAD)";
desc="$($GIT describe --tags --long --match='[0-9]*.[0-9]*.[0-9]*' 2>/dev/null || echo '0.0.0-0')";
current_version="${desc%%-*}";
branch_type="${branch%/*}";
branch_short_name="${branch#*/}";

if [ "$branch_type" = "$current_version" ]; then
    branch_type="release";
fi;

major="${current_version%%.*}";
minor="${current_version%.*}";
minor="${minor#*.}";
patch="${current_version##*.}";
hops="${desc#$current_version-}";
hops="${hops%%-*}";

if [ "$branch_type" == "patch" ]; then
    patch=$((patch + 1));
else
    if [ "$branch_type" != "release" ]; then
        minor=$((minor + 1));
        patch="0";
    else
        if [ "$hops" != "0"  ]; then
            patch=$((patch + 1));
        fi;
    fi;
fi;

if [ "$branch_type" != "release" ]; then
    prerelease="-$branch_short_name-$hops";
else
    if [ "$hops" != "0" ]; then
        prerelease="-RC$hops";
    else
        prerelease="";
    fi;
fi;

echo "$major.$minor.$patch$prerelease";
