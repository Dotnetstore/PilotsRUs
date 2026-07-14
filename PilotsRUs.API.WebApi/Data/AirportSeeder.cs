using Microsoft.EntityFrameworkCore;

namespace PilotsRUs.API.WebApi.Data;

/// <summary>
/// Seeds airports. Runs unconditionally (every environment), idempotent - same pattern as the other
/// reference-data seeders. Must run after CountrySeeder - resolves CountryId by IsoAlpha2Code.
/// </summary>
public static class AirportSeeder
{
    // Major airports worldwide. Each row references its Country by ISO alpha-2 code (e.g. "SE" for Sweden)
    // rather than a raw Guid, resolved to the actual CountryId at seed time.
    private static readonly (string Name, string IcaoCode, string? IataCode, string City, string CountryAlpha2)[] SeedAirports =
    [
        // North America
        ("Hartsfield-Jackson Atlanta International", "KATL", "ATL", "Atlanta", "US"),
        ("Dallas/Fort Worth International", "KDFW", "DFW", "Dallas", "US"),
        ("Denver International", "KDEN", "DEN", "Denver", "US"),
        ("Chicago O'Hare International", "KORD", "ORD", "Chicago", "US"),
        ("Los Angeles International", "KLAX", "LAX", "Los Angeles", "US"),
        ("New York John F. Kennedy International", "KJFK", "JFK", "New York", "US"),
        ("San Francisco International", "KSFO", "SFO", "San Francisco", "US"),
        ("Las Vegas Harry Reid International", "KLAS", "LAS", "Las Vegas", "US"),
        ("Phoenix Sky Harbor International", "KPHX", "PHX", "Phoenix", "US"),
        ("Miami International", "KMIA", "MIA", "Miami", "US"),
        ("Toronto Pearson International", "CYYZ", "YYZ", "Toronto", "CA"),
        ("Vancouver International", "CYVR", "YVR", "Vancouver", "CA"),
        ("Mexico City International", "MMMX", "MEX", "Mexico City", "MX"),
        ("Cancún International", "MMUN", "CUN", "Cancún", "MX"),

        // South America
        ("São Paulo/Guarulhos International", "SBGR", "GRU", "São Paulo", "BR"),
        ("Rio de Janeiro/Galeão International", "SBGL", "GIG", "Rio de Janeiro", "BR"),
        ("Buenos Aires Ministro Pistarini", "SAEZ", "EZE", "Buenos Aires", "AR"),
        ("Santiago de Chile International", "SCEL", "SCL", "Santiago", "CL"),
        ("Lima Jorge Chávez International", "SPIM", "LIM", "Lima", "PE"),
        ("Bogotá El Dorado International", "SKBO", "BOG", "Bogotá", "CO"),
        ("Caracas La Carlota", "SVCS", "CCS", "Caracas", "VE"),

        // Europe
        ("London Heathrow", "EGLL", "LHR", "London", "GB"),
        ("London Gatwick", "EGKK", "LGW", "London", "GB"),
        ("Paris Charles de Gaulle", "LFPG", "CDG", "Paris", "FR"),
        ("Paris Le Bourget", "LFPB", "LBG", "Paris", "FR"),
        ("Frankfurt am Main", "EDDF", "FRA", "Frankfurt", "DE"),
        ("Berlin Brandenburg", "EDDB", "BER", "Berlin", "DE"),
        ("Munich Franz Josef Strauss", "EDDM", "MUC", "Munich", "DE"),
        ("Amsterdam Airport Schiphol", "EHAM", "AMS", "Amsterdam", "NL"),
        ("Rome Fiumicino", "LIRF", "FCO", "Rome", "IT"),
        ("Milan Malpensa", "LIMC", "MXP", "Milan", "IT"),
        ("Madrid-Barajas", "LEMD", "MAD", "Madrid", "ES"),
        ("Barcelona-El Prat", "LEIB", "BCN", "Barcelona", "ES"),
        ("Istanbul Airport", "LTFM", "IST", "Istanbul", "TR"),
        ("Vienna International", "LOWW", "VIE", "Vienna", "AT"),
        ("Zurich Airport", "ZUMD", "ZRH", "Zurich", "CH"),
        ("Geneva Cointrin", "LSGG", "GVA", "Geneva", "CH"),
        ("Brussels-Zaventem", "EBBR", "BRU", "Brussels", "BE"),
        ("Copenhagen Airport", "EKCH", "CPH", "Copenhagen", "DK"),
        ("Stockholm Arlanda", "ESSA", "ARN", "Stockholm", "SE"),
        ("Oslo Gardermoen", "ENGM", "OSL", "Oslo", "NO"),
        ("Helsinki-Vantaa", "EFHK", "HEL", "Helsinki", "FI"),
        ("Dublin Airport", "EIDW", "DUB", "Dublin", "IE"),
        ("Athens International", "LGAV", "ATH", "Athens", "GR"),
        ("Prague Václav Havel", "LKPR", "PRG", "Prague", "CZ"),
        ("Warsaw Chopin", "EPWA", "WAW", "Warsaw", "PL"),
        ("Budapest Ferenc Liszt", "LHBP", "BUD", "Budapest", "HU"),
        ("Bucharest Henri Coandă", "LROP", "OTP", "Bucharest", "RO"),
        ("Sofia Airport", "LBSF", "SOF", "Sofia", "BG"),
        ("Belgrade Nikola Tesla", "LYBE", "BEG", "Belgrade", "RS"),
        ("Lisbon Portela", "LPPT", "LIS", "Lisbon", "PT"),
        ("London Stansted", "EGSS", "STN", "London", "GB"),
        ("London Luton", "EGGW", "LTN", "London", "GB"),
        ("London Southend", "EGMC", "SEN", "London", "GB"),

        // Middle East
        ("Dubai International", "OMDB", "DXB", "Dubai", "AE"),
        ("Abu Dhabi International", "OMAA", "AUH", "Abu Dhabi", "AE"),
        ("Doha Hamad International", "OTHH", "DOH", "Doha", "QA"),
        ("Riyadh King Fahd", "OERR", "RYD", "Riyadh", "SA"),
        ("Jeddah King Abdulaziz", "OEJN", "JED", "Jeddah", "SA"),
        ("Kuwait International", "OKBK", "KWI", "Kuwait City", "KW"),
        ("Tehran Imam Khomeini", "OIIE", "IKA", "Tehran", "IR"),
        ("Tel Aviv Ben Gurion", "LLBG", "TLV", "Tel Aviv", "IL"),
        ("Cairo International", "HECA", "CAI", "Cairo", "EG"),

        // Asia-Pacific
        ("Beijing Capital International", "ZBAA", "PEI", "Beijing", "CN"),
        ("Shanghai Pudong International", "ZSPD", "PVG", "Shanghai", "CN"),
        ("Hong Kong International", "VHHH", "HKG", "Hong Kong", "CN"),
        ("Singapore Changi", "WSSS", "SIN", "Singapore", "SG"),
        ("Tokyo Narita International", "RJAA", "NRT", "Tokyo", "JP"),
        ("Tokyo Haneda", "RJTT", "HND", "Tokyo", "JP"),
        ("Osaka International", "RJBB", "ITM", "Osaka", "JP"),
        ("Bangkok Suvarnabhumi", "VTBS", "BKK", "Bangkok", "TH"),
        ("Manila Ninoy Aquino International", "RPLL", "MNL", "Manila", "PH"),
        ("Kuala Lumpur International", "WMKK", "KUL", "Kuala Lumpur", "MY"),
        ("Jakarta Soekarno-Hatta", "WIII", "CGK", "Jakarta", "ID"),
        ("Sydney Kingsford Smith", "YSSY", "SYD", "Sydney", "AU"),
        ("Melbourne Airport", "YMML", "MEL", "Melbourne", "AU"),
        ("New Delhi Indira Gandhi", "VIDP", "DEL", "New Delhi", "IN"),
        ("Mumbai Bombay", "VABB", "BOM", "Mumbai", "IN"),
        ("Bangalore Kempegowda", "VOBL", "BLR", "Bangalore", "IN"),
        ("Seoul Incheon International", "RKSI", "ICN", "Seoul", "KR"),
        ("Busan Gimhae", "RKPK", "PUS", "Busan", "KR"),
        ("Phuket International", "VTSP", "HKT", "Phuket", "TH"),
        ("Ho Chi Minh City Tan Son Nhat", "VVTS", "SGN", "Ho Chi Minh City", "VN"),
        ("Hanoi Noi Bai", "VVNB", "HAN", "Hanoi", "VN"),
        ("Taipei Taoyuan International", "RCTP", "TPE", "Taipei", "TW"),
        ("Auckland International", "NZAA", "AKL", "Auckland", "NZ"),
        ("Christchurch Airport", "NZCH", "CHC", "Christchurch", "NZ"),

        // Africa
        ("Johannesburg O. R. Tambo", "FAOR", "JNB", "Johannesburg", "ZA"),
        ("Cape Town International", "CTIA", "CPT", "Cape Town", "ZA"),
        ("Lagos Murtala Muhammed", "DNMM", "LOS", "Lagos", "NG"),
        ("Nairobi Jomo Kenyatta", "HKJK", "NBO", "Nairobi", "KE"),
        ("Casablanca Mohammed V", "GMMN", "CMN", "Casablanca", "MA"),
        ("Tunis-Carthage", "DTTA", "TUN", "Tunis", "TN"),
        ("Addis Ababa Bole", "HAAB", "ADD", "Addis Ababa", "ET"),
        ("Accra Kotoka International", "DGAC", "ACC", "Accra", "GH")
    ];

    public static async Task SeedAsync(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        if (SeedAirports.Length == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var countryIdsByAlpha2 = await dbContext.Countries
            .ToDictionaryAsync(c => c.IsoAlpha2Code, c => c.Id, StringComparer.OrdinalIgnoreCase);

        var existingIcaoCodes = (await dbContext.Airports.Select(a => a.IcaoCode).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingIataCodes = (await dbContext.Airports.Where(a => a.IataCode != null).Select(a => a.IataCode!).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = new List<Airport>();
        foreach (var (name, icao, iata, city, countryAlpha2) in SeedAirports)
        {
            // Checks against both already-committed rows AND codes already queued earlier in this same
            // pass - guards against accidental duplicate ICAO/IATA codes within SeedAirports itself (a raw
            // literal array with no compile-time uniqueness check) causing an unhandled unique-index
            // violation when AddRange + SaveChangesAsync runs, which would otherwise crash startup.
            if (!existingIcaoCodes.Add(icao))
            {
                continue;
            }

            if (iata is not null && !existingIataCodes.Add(iata))
            {
                continue;
            }

            if (!countryIdsByAlpha2.TryGetValue(countryAlpha2, out var countryId))
            {
                continue; // Country not found (typo, or not seeded) - skip rather than fail startup.
            }

            missing.Add(new Airport { Id = Guid.NewGuid(), Name = name, IcaoCode = icao, IataCode = iata, City = city, CountryId = countryId });
        }

        if (missing.Count == 0)
        {
            return;
        }

        dbContext.Airports.AddRange(missing);
        await dbContext.SaveChangesAsync();
    }
}
