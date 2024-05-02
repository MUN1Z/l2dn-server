﻿namespace L2Dn.Geometry;

public readonly record struct LocationHeading(Location3D Location, int Heading): ILocationHeading
{
    public LocationHeading(int x, int y, int z, int heading): this(new Location3D(x, y, z), heading)
    {
    }

    public int X => Location.X;
    public int Y => Location.Y;
    public int Z => Location.Z;

    public override string ToString() => $"({X}, {Y}, {Z}, Heading={Heading})";
}