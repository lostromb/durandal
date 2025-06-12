#!/bin/bash

g++ -shared psphinx_trigger.cpp -Wall -O2 -I ./include -lpocketsphinx -o libpsphinx_trigger.so