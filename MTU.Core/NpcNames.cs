namespace MTU.Core;

public static partial class NpcGenerator
{
    // Original name pools: human given/family names, Aslan given/clan names,
    // and Vargr given/pack names. These are flavour, not canonical race rules.
    public static string GenerateName(string race, string gender, Func<int>? d6 = null)
    {
        if (!NpcRaces.SupportsGeneration(race))
            throw new ArgumentException("This race requires manual entry.");
        d6 ??= () => Random.Shared.Next(1, 7);
        var female = gender.Trim().Equals("Female", StringComparison.OrdinalIgnoreCase);
        string[] given;
        string[] family;
        switch (race.Trim().ToUpperInvariant())
        {
            case "ASLAN":
                given = female
                    ?
                    [
                        "Aihya",
                        "Fteira",
                        "Hraia",
                        "Ihlei",
                        "Khaiya",
                        "Raihea",
                        "Sairi",
                        "Seihya",
                        "Tailea",
                        "Tlaiya",
                        "Yaei",
                        "Yuhra",
                    ]
                    :
                    [
                        "Aehro",
                        "Ftao",
                        "Hrakao",
                        "Hroal",
                        "Iykhao",
                        "Khaor",
                        "Rahl",
                        "Sahro",
                        "Syoakh",
                        "Tlahro",
                        "Yaokh",
                        "Yuhal",
                    ];
                family =
                [
                    "Ahroakh",
                    "Fteirakh",
                    "Hlaokh",
                    "Hraiyal",
                    "Ihkoal",
                    "Khaileir",
                    "Raohte",
                    "Sahiyal",
                    "Syoahri",
                    "Tlaukhao",
                    "Yaeokh",
                    "Yuhai",
                ];
                break;
            case "VARGR":
                given = female
                    ?
                    [
                        "Aezha",
                        "Dzara",
                        "Eghra",
                        "Ghaela",
                        "Gzora",
                        "Khaega",
                        "Ouzha",
                        "Rraela",
                        "Thazha",
                        "Uerra",
                        "Vaegha",
                        "Zhaera",
                    ]
                    :
                    [
                        "Aekhaz",
                        "Dzaerr",
                        "Egzorg",
                        "Ghaez",
                        "Gzarr",
                        "Khaeg",
                        "Ouzorr",
                        "Rrakha",
                        "Thaegz",
                        "Uergh",
                        "Vaekhor",
                        "Zhaerr",
                    ];
                family =
                [
                    "Akhaez",
                    "Dzorg",
                    "Egzha",
                    "Ghaergh",
                    "Gzakorr",
                    "Khaeruz",
                    "Oerghaz",
                    "Rrazagh",
                    "Thaezorr",
                    "Uekhaz",
                    "Vragha",
                    "Zhaekorr",
                ];
                break;
            default:
                given = female
                    ?
                    [
                        "Amara",
                        "Anika",
                        "Celeste",
                        "Elena",
                        "Farah",
                        "Hana",
                        "Imani",
                        "Leila",
                        "Mira",
                        "Nadia",
                        "Priya",
                        "Zara",
                    ]
                    :
                    [
                        "Adrian",
                        "Arun",
                        "Darius",
                        "Elias",
                        "Hassan",
                        "Jonas",
                        "Kenji",
                        "Luca",
                        "Mateo",
                        "Nolan",
                        "Omar",
                        "Viktor",
                    ];
                family =
                [
                    "Adler",
                    "Chen",
                    "Daram",
                    "Haddad",
                    "Ishikawa",
                    "Kovacs",
                    "Mensah",
                    "Navarro",
                    "Okafor",
                    "Patel",
                    "Rahman",
                    "Voss",
                ];
                break;
        }
        // Twelve entries divide the 36 equally likely D66 combinations evenly.
        string Pick(string[] pool) => pool[((Roll(d6) - 1) * 6 + Roll(d6) - 1) % pool.Length];
        return $"{Pick(given)} {Pick(family)}";
    }
}
