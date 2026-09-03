using System;

namespace BimMep.Core.Geometry;

/// <summary>
/// Facade simplifiee aux primitives geometriques. En production, ces types s'appuient sur
/// le wrapper natif OpenCascade (libbimgeo, cf. docs/bim-mep-platform/11-specifications-techniques.md §15.2)
/// pour toute geometrie BRep ; les types ci-dessous suffisent pour le routage et le clash detection
/// analytiques (boites englobantes, segments) presentes dans ce dossier d'exemples.
/// </summary>
public readonly struct Point3D
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public Point3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double DistanceTo(Point3D other)
    {
        double dx = X - other.X, dy = Y - other.Y, dz = Z - other.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public Point3D Add(Vector3D v) => new(X + v.X, Y + v.Y, Z + v.Z);

    public override string ToString() => $"({X:F3}, {Y:F3}, {Z:F3})";
}

public readonly struct Vector3D
{
    public double X { get; }
    public double Y { get; }
    public double Z { get; }

    public Vector3D(double x, double y, double z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    public Vector3D Normalized()
    {
        double len = Length;
        if (len < 1e-9) return new Vector3D(0, 0, 0);
        return new Vector3D(X / len, Y / len, Z / len);
    }

    public static Vector3D operator *(Vector3D v, double s) => new(v.X * s, v.Y * s, v.Z * s);
    public static Vector3D operator +(Vector3D a, Vector3D b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
}

/// <summary>
/// Transformation rigide (translation + rotation). La rotation est simplifiee a un angle de lacet
/// (yaw) autour de Z, suffisant pour l'implantation de troncons MEP a plat ; une implementation
/// de production porte un quaternion complet.
/// </summary>
public readonly struct Transform3D
{
    public Point3D Origin { get; }
    public double YawRadians { get; }

    public Transform3D(Point3D origin, double yawRadians)
    {
        Origin = origin;
        YawRadians = yawRadians;
    }

    public static Transform3D Identity => new(new Point3D(0, 0, 0), 0);
}

/// <summary>
/// Boite englobante axis-aligned (AABB), unite de base du pre-filtrage de clash detection (§15.6)
/// et des noeuds de la BVH.
/// </summary>
public readonly struct AxisAlignedBox
{
    public Point3D Min { get; }
    public Point3D Max { get; }

    public AxisAlignedBox(Point3D min, Point3D max)
    {
        Min = min;
        Max = max;
    }

    public static AxisAlignedBox FromCenterExtent(Point3D center, double halfWidth, double halfHeight, double halfDepth)
    {
        var min = new Point3D(center.X - halfWidth, center.Y - halfDepth, center.Z - halfHeight);
        var max = new Point3D(center.X + halfWidth, center.Y + halfDepth, center.Z + halfHeight);
        return new AxisAlignedBox(min, max);
    }

    public bool Intersects(AxisAlignedBox other) =>
        Min.X <= other.Max.X && Max.X >= other.Min.X &&
        Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
        Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;

    /// <summary>Profondeur de penetration selon l'axe de moindre chevauchement (approximation pour le clash "dur").</summary>
    public double PenetrationDepth(AxisAlignedBox other)
    {
        if (!Intersects(other)) return 0.0;
        double overlapX = Math.Min(Max.X, other.Max.X) - Math.Max(Min.X, other.Min.X);
        double overlapY = Math.Min(Max.Y, other.Max.Y) - Math.Max(Min.Y, other.Min.Y);
        double overlapZ = Math.Min(Max.Z, other.Max.Z) - Math.Max(Min.Z, other.Min.Z);
        return Math.Min(overlapX, Math.Min(overlapY, overlapZ));
    }

    public AxisAlignedBox ExpandedBy(double margin)
    {
        var marginVec = new Vector3D(margin, margin, margin);
        return new AxisAlignedBox(Min.Add(marginVec * -1), Max.Add(marginVec));
    }

    public AxisAlignedBox Union(AxisAlignedBox other) => new(
        new Point3D(Math.Min(Min.X, other.Min.X), Math.Min(Min.Y, other.Min.Y), Math.Min(Min.Z, other.Min.Z)),
        new Point3D(Math.Max(Max.X, other.Max.X), Math.Max(Max.Y, other.Max.Y), Math.Max(Max.Z, other.Max.Z)));

    public Point3D Center => new(
        (Min.X + Max.X) / 2.0,
        (Min.Y + Max.Y) / 2.0,
        (Min.Z + Max.Z) / 2.0);

    public double SurfaceArea
    {
        get
        {
            double dx = Max.X - Min.X, dy = Max.Y - Min.Y, dz = Max.Z - Min.Z;
            return 2.0 * (dx * dy + dy * dz + dz * dx);
        }
    }
}
