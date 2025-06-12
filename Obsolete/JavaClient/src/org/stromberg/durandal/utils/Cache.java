/*
 * To change this template, choose Tools | Templates
 * and open the template in the editor.
 */
package org.stromberg.durandal.utils;

import java.util.Calendar;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Map;
import java.util.UUID;

/**
 *
 * @author lostromb
 */
public class Cache<T>
{
    private Map<String, CachedItem<T>> cache;
    private int minutesToCache = 1;

    public Cache(int expirationTimeInMinutes)
    {
        cache = new HashMap<String, CachedItem<T>>();
        minutesToCache = expirationTimeInMinutes;
    }

    public String Store(T item)
    {
        ClearOldEntries();
        UUID newId = UUID.randomUUID();
        String key = newId.toString(); //Long.toHexString(newId.getMostSignificantBits()) + Long.toHexString(newId.getLeastSignificantBits());
        CachedItem<T> thing = new CachedItem<T>(item, minutesToCache);
        cache.put(key, thing);
        return key;
    }

    public T Retrieve(String key)
    {
        if (cache.containsKey(key))
            return cache.get(key).Value;
        return null;
    }

    private void ClearOldEntries()
    {
        if (!cache.isEmpty())
        {
            long now = Calendar.getInstance().getTimeInMillis();
            HashSet<String> itemsToRemove = new HashSet<String>();
            for (String key : cache.keySet())
            {
                if (cache.get(key).ExpireTime < now)
                {
                    itemsToRemove.add(key);
                }
            }
            for (String key : itemsToRemove)
            {
                cache.remove(key);
            }
        }
    }
}
