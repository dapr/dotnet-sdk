namespace Dapr.Actors.Next.Examples.Migration;

public sealed class GraduatedCartStateV2
{
    public List<CartLine> Lines { get; init; } = [];

    public int TotalQuantity { get; init; }

    public string Currency { get; init; } = "USD";
}

public sealed class GraduatedCartState
{
    public List<CartLine> Lines { get; init; } = [];

    public int TotalQuantity { get; init; }
}

public sealed class RenamedState
{
    public string FirstName { get; init; } = "";

    public string LastName { get; init; } = "";
}

public sealed class RenamedStateV2
{
    public string DisplayName { get; init; } = "";
}

public sealed class MyStateV2
{
    public string Name { get; init; } = "";

    public int Age { get; init; }
}

public sealed class MyStateV3
{
    public string Name { get; init; } = "";

    public int Age { get; init; }

    public bool Active { get; init; }
}

public sealed class MyState
{
    public string Name { get; init; } = "";
}

public sealed record CartLine(string Sku, int Quantity);

public sealed class CartStateV1
{
    public List<string> Skus { get; init; } = [];
}

public sealed class CartStateV3
{
    public List<CartLine> Lines { get; init; } = [];

    public int TotalQuantity { get; init; }
}

public sealed class CartStateV2
{
    public List<CartLine> Lines { get; init; } = [];
}
