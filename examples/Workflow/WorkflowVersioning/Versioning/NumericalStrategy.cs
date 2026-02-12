using System.Globalization;
using Dapr.Workflow.Versioning;

namespace WorkflowVersioning.Versioning;

public sealed class NumericalStrategy : IWorkflowVersionStrategy
{
    public bool TryParse(string typeName, out string canonicalName, out string version)
    {
        canonicalName = string.Empty;
        version = string.Empty;

        if (string.IsNullOrWhiteSpace(typeName))
            return false;

        // Extract trailing digits as the version
        int i = typeName.Length - 1;
        while (i >= 0 && char.IsDigit(typeName[i]))
            i--;

        if (i < typeName.Length - 1)
        {
            canonicalName = typeName[..(i + 1)];
            version = typeName[(i + 1)..];
        }
        else
        {
            canonicalName = typeName;
            version = "0";
        }

        return true;
    }

    public int Compare(string? v1, string? v2)
    {
        var left = int.Parse(v1 ?? "0", CultureInfo.InvariantCulture);
        var right = int.Parse(v2 ?? "0", CultureInfo.InvariantCulture);
        
        return left.CompareTo(right);    
    }
}
