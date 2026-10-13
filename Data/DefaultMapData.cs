using UfgEnvios.Models;

namespace UfgEnvios.Data;

public static class DefaultMapData
{
    // Coordenadas sobre la imagen de referencia (775 × 780). Cada municipio es un vértice.
    public static List<HubNode> GetDefaultHubs()
    {
        var nodes = new List<HubNode>
        {
        Node("QUE", "Quetzaltenango", 619, 420, HubType.AirHub),
        Node("SAL", "Salcajá", 691, 378),
        Node("OLI", "Olintepeque", 610, 364),
        Node("SIJ", "San Carlos Sija", 579, 226),
        Node("SIB", "Sibilia", 514, 204),
        Node("CAB", "Cabricán", 499, 133),
        Node("CAJ", "Cajolá", 526, 305),
        Node("SIG", "San Miguel Sigüilá", 551, 347),
        Node("OST", "San Juan Ostuncalco", 478, 376),
        Node("MAT", "San Mateo", 575, 410),
        Node("CHI", "Concepción Chiquirichapa", 539, 412),
        Node("MAR", "San Martín Sacatepéquez", 489, 456),
        Node("ALM", "Almolonga", 671, 445),
        Node("CAN", "Cantel", 715, 442),
        Node("HUI", "Huitán", 507, 255),
        Node("ZUN", "Zunil", 638, 493),
        Node("COL", "Colomba Costa Cuca", 384, 541),
        Node("FRA", "San Francisco La Unión", 619, 321),
        Node("PAL", "El Palmar", 549, 559),
        Node("COA", "Coatepeque", 158, 613),
        Node("GEN", "Génova", 286, 716),
        Node("FLO", "Flores Costa Cuca", 303, 612),
        Node("ESP", "La Esperanza", 577, 375),
        Node("PAH", "Palestina de los Altos", 478, 312)
        };
        // Un punto blanco interior por cada contorno de la imagen de referencia.
        var regionSeeds = new (int X, int Y)[]
        {
            (622, 433), (693, 369), (620, 356), (588, 161),
            (526, 198), (507, 135), (542, 310), (555, 344),
            (470, 373), (572, 404), (536, 402), (491, 454),
            (671, 436), (716, 431), (515, 255), (660, 504),
            (404, 539), (612, 320), (550, 551), (177, 611),
            (312, 691), (294, 618), (578, 369), (479, 313)
        };
        for (var i = 0; i < nodes.Count; i++)
        {
            nodes[i].MapNumber = i + 1;
            nodes[i].RegionX = regionSeeds[i].X;
            nodes[i].RegionY = regionSeeds[i].Y;
        }
        return nodes;
    }

    private static HubNode Node(string id, string name, double x, double y, HubType type = HubType.GroundStation) =>
        new() { Id = id, Code = id, Name = name, X = x, Y = y, Type = type, Region = "Quetzaltenango", Capacity = "Simulada" };

    // Enlaces bidireccionales simulados entre municipios vecinos o próximos.
    // Los pesos no representan carreteras ni kilometrajes oficiales.
    public static List<RouteEdge> GetDefaultRoutes()
    {
        var routes = new List<RouteEdge>();
        void Add(string from, string to, double km)
        {
            routes.Add(new RouteEdge { FromId = from, ToId = to, DistanceKm = km });
            routes.Add(new RouteEdge { FromId = to, ToId = from, DistanceKm = km });
        }

        Add("QUE", "OLI", 7); Add("QUE", "SAL", 11); Add("QUE", "MAT", 5);
        Add("QUE", "ALM", 9); Add("QUE", "ZUN", 12); Add("QUE", "ESP", 6);
        Add("QUE", "CAN", 14); Add("SAL", "OLI", 10); Add("SAL", "CAN", 14);
        Add("OLI", "FRA", 7); Add("OLI", "ESP", 5); Add("OLI", "SIJ", 23);
        Add("SIJ", "SIB", 15); Add("SIJ", "FRA", 19); Add("SIB", "CAB", 14);
        Add("SIB", "HUI", 9); Add("HUI", "CAB", 12); Add("HUI", "CAJ", 10);
        Add("HUI", "SIJ", 17); Add("CAJ", "SIG", 8); Add("CAJ", "PAH", 10);
        Add("CAJ", "ESP", 12); Add("SIG", "ESP", 6); Add("SIG", "OST", 14);
        Add("SIG", "PAH", 13); Add("OST", "PAH", 13); Add("OST", "CHI", 13);
        Add("OST", "MAR", 15); Add("OST", "MAT", 16); Add("MAT", "ESP", 5);
        Add("MAT", "CHI", 6); Add("CHI", "MAR", 10); Add("MAR", "COL", 25);
        Add("MAR", "PAL", 24); Add("ALM", "CAN", 7); Add("ALM", "ZUN", 10);
        Add("CAN", "ZUN", 16); Add("ZUN", "PAL", 24); Add("PAL", "COL", 28);
        Add("COL", "FLO", 17); Add("COL", "COA", 33); Add("FLO", "COA", 25);
        Add("FLO", "GEN", 20); Add("GEN", "COA", 30);
        return routes;
    }
}
