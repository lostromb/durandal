#!/bin/bash

mv ./libpsphinx_trigger.so /usr/lib/libpsphinx_trigger.so.0.0.0
ln -s /usr/lib/libpsphinx_trigger.so.0.0.0 /usr/lib/libpsphinx_trigger.so.0
ln -s /usr/lib/libpsphinx_trigger.so.0 /usr/lib/libpsphinx_trigger.so