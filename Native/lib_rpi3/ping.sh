#!/bin/bash
ping -c4 192.168.7.1 > /dev/null

if [ $? != 0 ]
then
  /sbin/ifdown 'wlan0'
  sleep 5
  /sbin/ifup --force  'wlan0'
fi
