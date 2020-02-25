#!/bin/sh -
test -f rsync.cs && mcs -r:System.Net.Http -sdk:4.5 rsync.cs && mono rsync.exe $@ ; rm -f ./rsync.exe
mcs -r:System.Net.Http -sdk:4.5 yup_notify.cs && mono yup_notify.exe $@
