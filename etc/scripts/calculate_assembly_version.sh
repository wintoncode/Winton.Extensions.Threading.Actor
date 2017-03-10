#!/bin/bash

# Copyright (c) Winton. All rights reserved.
# Licensed under the Apache License, Version 2.0. See LICENCE in the project root for license information.

set -euo pipefail

GIT=$(which git)

# Outputs to stdout the the assembly version in the form major.0.0.0.
# If no version is passed in then one is determined using git describe --tags

desc_cmd="$GIT describe --tags";

case "$#" in
    1)
        desc="$1";
    ;;
    0)
        desc="$($desc_cmd)";
    ;;
    *)
        echo "Usage: $0 [<version>]";
        exit 1;
esac;

major="${desc%%.*}";

echo "$major.0.0.0";