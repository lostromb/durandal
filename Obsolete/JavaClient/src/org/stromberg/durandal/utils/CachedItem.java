package org.stromberg.durandal.utils;

import java.util.Calendar;

/**
 *
 * @author lostromb
 */
public class CachedItem<T>
{
    public T Value;
    public long StoreTime;
    public long ExpireTime;
    
    public CachedItem(T value, int minutesToExpire)
    {
        Value = value;
        StoreTime = Calendar.getInstance().getTimeInMillis();
        ExpireTime = StoreTime + (minutesToExpire * 60000);
    }
}
