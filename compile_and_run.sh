#!/bin/sh -
mcs -r:System.Net.Http -sdk:4.5 yup_notify.cs && mono yup_notify.exe $@
