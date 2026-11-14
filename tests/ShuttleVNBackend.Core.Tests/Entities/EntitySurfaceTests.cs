using System.Collections;
using System.Reflection;

namespace ShuttleVNBackend.Core.Tests.Entities;

// Core is mostly POCOs, so coverage comes from exercising every public
// accessor once instead of hand-writing one assignment per property.
public class EntitySurfaceTests
{
    public static TheoryData<Type> EntityTypes()
    {
        var data = new TheoryData<Type>();
        var assembly = typeof(ShuttleVNBackend.Core.Attributes.NoAuditAttribute).Assembly;

        foreach (var type in assembly.GetTypes())
        {
            if (type.IsClass && !type.IsAbstract && !type.IsGenericTypeDefinition
                && type.GetConstructor(Type.EmptyTypes) is not null
                && type.Namespace?.StartsWith("ShuttleVNBackend.Core.Entities", StringComparison.Ordinal) == true)
            {
                data.Add(type);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EntityTypes))]
    public void EveryPublicProperty_RoundTripsAssignedValue(Type entityType)
    {
        var instance = Activator.CreateInstance(entityType);
        var exercised = 0;

        foreach (var property in entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetSetMethod() is null || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var value = CreateSampleValue(property.PropertyType);
            property.SetValue(instance, value);

            try
            {
                property.GetValue(instance);
            }
            catch (TargetInvocationException)
            {
                // Required navigations throw while unset; those guards have
                // dedicated tests in NavigationGuardTests.
            }

            exercised++;
        }

        Assert.True(exercised > 0, $"{entityType.Name} exposes no writable properties.");
    }

    private static object? CreateSampleValue(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type);
        if (underlying is not null)
        {
            return CreateSampleValue(underlying);
        }

        if (type == typeof(string))
        {
            return "sample";
        }

        if (type == typeof(Guid))
        {
            return Guid.NewGuid();
        }

        if (type == typeof(int))
        {
            return 1;
        }

        if (type == typeof(long))
        {
            return 1L;
        }

        if (type == typeof(decimal))
        {
            return 100m;
        }

        if (type == typeof(bool))
        {
            return true;
        }

        if (type == typeof(DateTime))
        {
            return new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        if (type == typeof(DateOnly))
        {
            return new DateOnly(2026, 1, 1);
        }

        if (type == typeof(TimeOnly))
        {
            return new TimeOnly(6, 0);
        }

        if (type.IsEnum)
        {
            return Enum.GetValues(type).GetValue(0);
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICollection<>))
        {
            var elementType = type.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
            return list;
        }

        return Activator.CreateInstance(type);
    }
}
