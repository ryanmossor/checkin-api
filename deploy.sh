#!/usr/bin/env bash

TIMEZONE="$(timedatectl status | grep "zone" | awk '{print $3}')"
TIMEZONE="$TIMEZONE" docker compose up -d --build
