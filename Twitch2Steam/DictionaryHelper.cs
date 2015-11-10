using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Twitch2Steam
{
    public static class DictionaryHelper
    {
        //Extension method to let Dictionaries be nice 
        //Inspired by http://stackoverflow.com/questions/2601477/dictionary-returning-a-default-value-if-the-key-does-not-exist
        public static TValue GetValueOrInsertDefault<TKey, TValue>( this IDictionary<TKey, TValue> dictionary, TKey key)
        {
            TValue value;
            dictionary.TryGetValue(key, out value);
            if (value == null)
                value = Activator.CreateInstance<TValue>();
            dictionary[key] = value;

            return value;
        }

        public static bool IsEmpty<T>(this ICollection<T> list)
        {
            return list.Count == 0;
        }

        //TODO learn how to call
        public static TValue TestGetValueOrInsertDefault<TKey, TValue, TDefault>( this IDictionary<TKey, TValue> dictionary, TKey key)  where TDefault : TValue
        {
            TValue value;
            dictionary.TryGetValue(key, out value);
            if (value == null)
                value = Activator.CreateInstance<TDefault>();
            dictionary[key] = value;

            return value;
        }

        public static TValue GetValueOrInsertDefault<TKey, TValue>( this IDictionary<TKey, TValue> dictionary, TKey key, Type defaultType )
        {
            TValue value;
            dictionary.TryGetValue(key, out value);
            if (value == null)
                value = (TValue) Activator.CreateInstance(defaultType);
            dictionary[key] = value;

            return value;
        }

        public static TValue GetValueOrInsertDefault<TKey, TValue>( this IDictionary<TKey, TValue> dictionary, TKey key, TValue defaultValue )
        {
            TValue value;
            dictionary.TryGetValue(key, out value);
            if (value == null)
                value = defaultValue;
            dictionary[key] = value;

            return value;
        }

        public static TValue GetValueOrInsertDefault<TKey, TValue>( this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> defaultValueProvider )
        {
            TValue value;
            dictionary.TryGetValue(key, out value);
            if (value == null)
                value = defaultValueProvider();
            dictionary[key] = value;

            return value;
        }

    }
}