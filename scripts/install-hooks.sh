#!/bin/sh

set -e

git config core.hooksPath .githooks
printf '%s\n' "Git hooks enabled: .githooks"
