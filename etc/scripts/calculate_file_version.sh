#!/bin/bash

# Copyright (c) Winton. All rights reserved.
# Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

set -euo pipefail

GIT=$(which git)

# Tries to determine a version similar to that output by GitVersion

case "$#" in
    1)
        commitish="$1";
    ;;
    0)
        commitish="HEAD"
    ;;
    *)
        echo "Usage: $0 [<commit-ish>]";
        exit 1;
esac;

# Branch name:
# 1. If a tag is checked out or we're bang on a tag anyway then getting the exact match
#    will set branch to the version number. This will mean that below the branch is
#    treated as a release no matter what the true name of the branch is.
# 2. Failing that, try to get the branch name using symbolic-ref
# 3. ... but that won't work for detached heads so first try TRAVIS_BRANCH in case we're on Travis
# 4. ... and if this isn't Travis try Appveyor
# 4. ... and if this isn't Appveyor try to use git describe to get something even if it's just a sha
# 5. ... but sometimes despite the --always, even that  will fail so just output the commitish.
branch="$($GIT describe --tags --exact-match --match='[0-9]*.[0-9]*.[0-9]*' $commitish 2>/dev/null || $GIT symbolic-ref -q --short HEAD || echo ${TRAVIS_BRANCH:-})";

if [[ -z $branch ]]; then
    branch="${APPVEYOR_REPO_BRANCH}"
fi;

if [[ -z $branch ]]; then
    branch="$($GIT describe --all --exact-match --always $commitish 2>/dev/null || echo $commitish)";
fi;

desc="$($GIT describe --tags --long --match='[0-9]*.[0-9]*.[0-9]*' $commitish 2>/dev/null || echo '0.0.0-0')";
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
