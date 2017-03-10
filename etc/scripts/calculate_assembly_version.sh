#!/bin/bash

# Copyright (c) Winton. All rights reserved.
# Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

set -euo pipefail

GIT=$(which git)

# Outputs to stdout the the assembly version in the form major.0.0.0.

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

desc="$($GIT describe --tags --exact-match --match='[0-9]*.[0-9]*.[0-9]*' $commitish 2>/dev/null || echo "0")";
major="${desc%%.*}";

echo "$major.0.0.0";