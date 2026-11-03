using System.Reflection;
using RodajeIA.Web.Guiones;

namespace RodajeIA.Tests;

public class ArquitecturaTests
{
    private const string EspacioGuiones = "RodajeIA.Web.Guiones";

    private static readonly string[] EspaciosProhibidos =
    [
        "RodajeIA.Web.Generacion",
        "RodajeIA.Web.Servicios",
        "RodajeIA.Web.Datos",
        "RodajeIA.Web.Components",
    ];

    private const BindingFlags Todos =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static |
        BindingFlags.DeclaredOnly;

    [Fact]
    public void Guiones_NoReferenciaGeneracionServiciosNiDatos()
    {
        var tiposGuiones = typeof(GuionParser).Assembly.GetTypes()
            .Where(t => t.Namespace == EspacioGuiones || (t.Namespace?.StartsWith(EspacioGuiones + ".", StringComparison.Ordinal) ?? false))
            .ToList();
        Assert.NotEmpty(tiposGuiones);

        var violaciones = tiposGuiones
            .SelectMany(t => TiposReferenciados(t).Select(r => (Origen: t, Referido: r)))
            .Where(par => EsProhibido(par.Referido))
            .Select(par => $"{par.Origen.FullName} → {par.Referido.FullName}")
            .Distinct()
            .ToList();

        Assert.Empty(violaciones);
    }

    private static bool EsProhibido(Type tipo) =>
        tipo.Namespace is { } ns &&
        EspaciosProhibidos.Any(p => ns == p || ns.StartsWith(p + ".", StringComparison.Ordinal));

    /// <summary>
    /// Tipos que aparecen en la firma de <paramref name="tipo"/>: base, interfaces, atributos,
    /// campos, propiedades, eventos, parámetros y retornos de métodos y constructores, y variables
    /// locales de sus cuerpos; con sus argumentos genéricos, arreglos y punteros desplegados.
    /// </summary>
    private static IEnumerable<Type> TiposReferenciados(Type tipo)
    {
        var directos = new List<Type>();
        if (tipo.BaseType is { } baseType)
        {
            directos.Add(baseType);
        }

        directos.AddRange(tipo.GetInterfaces());
        directos.AddRange(tipo.GetCustomAttributesData().Select(a => a.AttributeType));
        directos.AddRange(tipo.GetFields(Todos).Select(f => f.FieldType));
        directos.AddRange(tipo.GetProperties(Todos).Select(p => p.PropertyType));
        directos.AddRange(tipo.GetEvents(Todos).Select(e => e.EventHandlerType).OfType<Type>());

        var metodos = tipo.GetMethods(Todos).Cast<MethodBase>().Concat(tipo.GetConstructors(Todos));
        foreach (var metodo in metodos)
        {
            if (metodo is MethodInfo info)
            {
                directos.Add(info.ReturnType);
            }

            directos.AddRange(metodo.GetParameters().Select(p => p.ParameterType));
            var cuerpo = metodo.GetMethodBody();
            if (cuerpo is not null)
            {
                directos.AddRange(cuerpo.LocalVariables.Select(v => v.LocalType));
            }
        }

        return directos.SelectMany(Desplegar);
    }

    private static IEnumerable<Type> Desplegar(Type tipo)
    {
        if (tipo.HasElementType && tipo.GetElementType() is { } elemento)
        {
            foreach (var t in Desplegar(elemento))
            {
                yield return t;
            }

            yield break;
        }

        yield return tipo;
        if (tipo.IsGenericType)
        {
            foreach (var argumento in tipo.GetGenericArguments().SelectMany(Desplegar))
            {
                yield return argumento;
            }
        }
    }
}
