using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace CrewChiefV4.GameState
{
    public class ReflectionGameStateAccessor
    {
        // cached accessor functions, lazily created and reused for the life of the application
        private static Dictionary<string, Func<object, object>> accessorFunctions = new Dictionary<string, Func<object, object>>();

        private static HashSet<string> blacklistedPaths = new HashSet<string>();
        
        public static object getPropertyValue(object root, string propName)
        {
            if (propName == null)
            {
                return null;
            }
            // first check if it's an array index / accessor we're looking up and change the propname & behaviour if necessary.
            // Note we only support array access for the final bit of the property lookup - i.e.
            // "PositionAndMotionData.WorldPosition[2]" is OK, "PositionAndMotionData.WorldPosition[2].SomeOtherNestedProperty" is not
            ArrayAccessor arrayAccessor = null;
            int propNameLength = propName.Length;
            if (propNameLength == 0)
            {
                return null;
            }
            string cachePath = root.GetType().FullName + "." + propName;
            if (blacklistedPaths.Contains(cachePath))
            {
                return null;
            }
            if (propName[propNameLength - 1] == ']')
            {
                int arrayStartBracketLocation = propName.LastIndexOf('[');
                if (arrayStartBracketLocation == -1)
                {
                    blacklistedPaths.Add(cachePath);
                    Log.Error("No matching [ char in propname " + propName);
                    return null;
                }
                string arrayIndexString = propName.Substring(arrayStartBracketLocation + 1, propNameLength - arrayStartBracketLocation - 2);
                arrayAccessor = ArrayAccessor.FromString(arrayIndexString);
                if (arrayAccessor == null)
                {
                    Log.Error("Unable to create array index from bracketed value " + arrayIndexString + " in propName " + propName + ". Must be an integer, ALL or LAST");
                    blacklistedPaths.Add(cachePath);
                    return null;
                }
                propName = propName.Substring(0, arrayStartBracketLocation);
            }
            // get the accessorFunction from the cache if we've already created it
            Func<object, object> accessorFunction;
            if (accessorFunctions.TryGetValue(cachePath, out accessorFunction))
            {
                // invoke it, using the array accessor if necessary
                return InvokeAccessor(root, arrayAccessor, accessorFunction);
            }
            else
            {
                // try to create it and cache + invoke it if we can:
                accessorFunction = CreateAccessorFunction(root.GetType(), propName);
                if (accessorFunction == null)
                {
                    blacklistedPaths.Add(cachePath);
                }
                else
                {
                    accessorFunctions[cachePath] = accessorFunction;
                    return InvokeAccessor(root, arrayAccessor, accessorFunction);
                }
            }
            return null;
        }

        private static object InvokeAccessor(object root, ArrayAccessor arrayAccessor, Func<object, object> accessorFunction)
        {
            try
            {
                if (arrayAccessor == null)
                {
                    return accessorFunction.Invoke(root);
                }
                else
                {
                    return arrayAccessor.getValue(((IList)accessorFunction.Invoke(root)));
                }
            }
            catch (Exception)
            {
                // if we're accessing a property who's containing object is null the accessor will throw an NPE
                return null;
            }
        }

        private static Func<object, object> CreateAccessorFunction(Type rootType, string path)
        {
            try
            {
                var previousType = rootType;
                var param = Expression.Parameter(typeof(object));
                Expression access = Expression.Convert(param, rootType);
                string[] propNames = path.Split(new char[] { '.' });
                for (int i=0; i<propNames.Length; i++)
                {
                    string propName = propNames[i];
                    MemberInfo member = previousType.GetMember(propName)[0];
                    access = Expression.MakeMemberAccess(access, member);
                    if (i < propNames.Length - 1)
                    {
                        if (member.MemberType == MemberTypes.Field)
                        {
                            previousType = ((FieldInfo)member).FieldType;
                        }
                        else if (member.MemberType == MemberTypes.Property)
                        {
                            previousType = ((PropertyInfo)member).PropertyType;
                        }
                        else
                        {
                            Log.Error("Property " + propName + " is not a Field or Property");
                            return null;
                        }
                    }
                }
                var lambda = Expression.Lambda<Func<object, object>>(
                    Expression.Convert(access, typeof(object)),
                    param
                ).Compile();
                return lambda;
            }
            catch (Exception e)
            {
                // don't allow this accessor to be re-evaluated
                Log.Error("Object path " + path + " cannot be processed: " + e.Message);
                return null;
            }
        }

        private class ArrayAccessor
        {
            const string ALL = "all";
            const string LAST = "last";
            public int index = -1;
            public bool lastElement = false;
            public bool allElements = false;
            ArrayAccessor(int index, bool lastElement, bool allElements)
            {
                this.index = index;
                this.lastElement = lastElement;
                this.allElements = allElements;
            }
            public static ArrayAccessor FromString(string str)
            {
                if (ALL.Equals(str, StringComparison.InvariantCultureIgnoreCase))
                {
                    return new ArrayAccessor(-1, false, true);
                }
                if (LAST.Equals(str, StringComparison.InvariantCultureIgnoreCase))
                {
                    return new ArrayAccessor(-1, true, false);
                }
                if (int.TryParse(str, out var index))
                {
                    return new ArrayAccessor(index, false, false);
                }
                return null;
            }
            public object getValue(IList list)
            {
                if (list != null)
                {
                    int count = list.Count;
                    if (allElements)
                    {
                        var items = new string[count];
                        for (int i = 0; i < count; i++)
                        {
                            items[i] = list[i].ToString();
                        }
                        return "[" + string.Join(",", items) + "]";
                    }
                    else if (lastElement)
                    {
                        if (count > 0)
                        {
                            return list[count - 1];
                        }
                    }
                    else if (count > index)
                    {
                        return list[index];
                    }
                }
                return null;
            }
        }
    }
}
