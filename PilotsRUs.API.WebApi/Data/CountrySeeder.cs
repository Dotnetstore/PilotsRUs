using Microsoft.EntityFrameworkCore;

namespace PilotsRUs.API.WebApi.Data;

/// <summary>
/// Seeds the ISO 3166-1 country list. Runs unconditionally (every environment), idempotent - same pattern
/// as <see cref="ManufacturerSeeder"/>/<see cref="AircraftModelSeeder"/>. No ordering dependency on the
/// other seeders - Country has no FK to anything.
/// </summary>
public static class CountrySeeder
{
    // The commonly-understood "countries of the world" list (195 sovereign states: Name, IsoAlpha2Code,
    // IsoAlpha3Code) - not the full ~249-entry ISO 3166-1 registry, which also includes dependent
    // territories (Puerto Rico, Hong Kong, etc.).
    private static readonly (string Name, string Alpha2, string Alpha3)[] SeedCountries =
    [
        ("Afghanistan", "AF", "AFG"), ("Albania", "AL", "ALB"), ("Algeria", "DZ", "DZA"), ("Andorra", "AD", "AND"),
        ("Angola", "AO", "AGO"), ("Antigua and Barbuda", "AG", "ATG"), ("Argentina", "AR", "ARG"), ("Armenia", "AM", "ARM"),
        ("Australia", "AU", "AUS"), ("Austria", "AT", "AUT"), ("Azerbaijan", "AZ", "AZE"), ("Bahamas", "BS", "BHS"),
        ("Bahrain", "BH", "BHR"), ("Bangladesh", "BD", "BGD"), ("Barbados", "BB", "BRB"), ("Belarus", "BY", "BLR"),
        ("Belgium", "BE", "BEL"), ("Belize", "BZ", "BLZ"), ("Benin", "BJ", "BEN"), ("Bhutan", "BT", "BTN"),
        ("Bolivia", "BO", "BOL"), ("Bosnia and Herzegovina", "BA", "BIH"), ("Botswana", "BW", "BWA"), ("Brazil", "BR", "BRA"),
        ("Brunei", "BN", "BRN"), ("Bulgaria", "BG", "BGR"), ("Burkina Faso", "BF", "BFA"), ("Burundi", "BI", "BDI"),
        ("Cambodia", "KH", "KHM"), ("Cameroon", "CM", "CMR"), ("Canada", "CA", "CAN"), ("Cape Verde", "CV", "CPV"),
        ("Central African Republic", "CF", "CAF"), ("Chad", "TD", "TCD"), ("Chile", "CL", "CHL"), ("China", "CN", "CHN"),
        ("Colombia", "CO", "COL"), ("Comoros", "KM", "COM"), ("Congo", "CG", "COG"), ("Costa Rica", "CR", "CRI"),
        ("Côte d'Ivoire", "CI", "CIV"), ("Croatia", "HR", "HRV"), ("Cuba", "CU", "CUB"), ("Cyprus", "CY", "CYP"),
        ("Czech Republic", "CZ", "CZE"), ("Denmark", "DK", "DNK"), ("Djibouti", "DJ", "DJI"), ("Dominica", "DM", "DMA"),
        ("Dominican Republic", "DO", "DOM"), ("East Timor", "TL", "TLS"), ("Ecuador", "EC", "ECU"), ("Egypt", "EG", "EGY"),
        ("El Salvador", "SV", "SLV"), ("Equatorial Guinea", "GQ", "GNQ"), ("Eritrea", "ER", "ERI"), ("Estonia", "EE", "EST"),
        ("Eswatini", "SZ", "SWZ"), ("Ethiopia", "ET", "ETH"), ("Fiji", "FJ", "FJI"), ("Finland", "FI", "FIN"),
        ("France", "FR", "FRA"), ("Gabon", "GA", "GAB"), ("Gambia", "GM", "GMB"), ("Georgia", "GE", "GEO"),
        ("Germany", "DE", "DEU"), ("Ghana", "GH", "GHA"), ("Greece", "GR", "GRC"), ("Grenada", "GD", "GRD"),
        ("Guatemala", "GT", "GTM"), ("Guinea", "GN", "GIN"), ("Guinea-Bissau", "GW", "GNB"), ("Guyana", "GY", "GUY"),
        ("Haiti", "HT", "HTI"), ("Honduras", "HN", "HND"), ("Hungary", "HU", "HUN"), ("Iceland", "IS", "ISL"),
        ("India", "IN", "IND"), ("Indonesia", "ID", "IDN"), ("Iran", "IR", "IRN"), ("Iraq", "IQ", "IRQ"),
        ("Ireland", "IE", "IRL"), ("Israel", "IL", "ISR"), ("Italy", "IT", "ITA"), ("Jamaica", "JM", "JAM"),
        ("Japan", "JP", "JPN"), ("Jordan", "JO", "JOR"), ("Kazakhstan", "KZ", "KAZ"), ("Kenya", "KE", "KEN"),
        ("Kiribati", "KI", "KIR"), ("Korea, North", "KP", "PRK"), ("Korea, South", "KR", "KOR"), ("Kuwait", "KW", "KWT"),
        ("Kyrgyzstan", "KG", "KGZ"), ("Laos", "LA", "LAO"), ("Latvia", "LV", "LVA"), ("Lebanon", "LB", "LBN"),
        ("Lesotho", "LS", "LSO"), ("Liberia", "LR", "LBR"), ("Libya", "LY", "LBY"), ("Liechtenstein", "LI", "LIE"),
        ("Lithuania", "LT", "LTU"), ("Luxembourg", "LU", "LUX"), ("Madagascar", "MG", "MDG"), ("Malawi", "MW", "MWI"),
        ("Malaysia", "MY", "MYS"), ("Maldives", "MV", "MDV"), ("Mali", "ML", "MLI"), ("Malta", "MT", "MLT"),
        ("Marshall Islands", "MH", "MHL"), ("Mauritania", "MR", "MRT"), ("Mauritius", "MU", "MUS"), ("Mexico", "MX", "MEX"),
        ("Micronesia", "FM", "FSM"), ("Moldova", "MD", "MDA"), ("Monaco", "MC", "MCO"), ("Mongolia", "MN", "MNG"),
        ("Montenegro", "ME", "MNE"), ("Morocco", "MA", "MAR"), ("Mozambique", "MZ", "MOZ"), ("Myanmar", "MM", "MMR"),
        ("Namibia", "NA", "NAM"), ("Nauru", "NR", "NRU"), ("Nepal", "NP", "NPL"), ("Netherlands", "NL", "NLD"),
        ("New Zealand", "NZ", "NZL"), ("Nicaragua", "NI", "NIC"), ("Niger", "NE", "NER"), ("Nigeria", "NG", "NGA"),
        ("North Macedonia", "MK", "MKD"), ("Norway", "NO", "NOR"), ("Oman", "OM", "OMN"), ("Pakistan", "PK", "PAK"),
        ("Palau", "PW", "PLW"), ("Palestine", "PS", "PSE"), ("Panama", "PA", "PAN"), ("Papua New Guinea", "PG", "PNG"),
        ("Paraguay", "PY", "PRY"), ("Peru", "PE", "PER"), ("Philippines", "PH", "PHL"), ("Poland", "PL", "POL"),
        ("Portugal", "PT", "PRT"), ("Qatar", "QA", "QAT"), ("Romania", "RO", "ROU"), ("Russia", "RU", "RUS"),
        ("Rwanda", "RW", "RWA"), ("Saint Kitts and Nevis", "KN", "KNA"), ("Saint Lucia", "LC", "LCA"),
        ("Saint Vincent and the Grenadines", "VC", "VCT"), ("Samoa", "WS", "WSM"), ("San Marino", "SM", "SMR"),
        ("Sao Tome and Principe", "ST", "STP"), ("Saudi Arabia", "SA", "SAU"), ("Senegal", "SN", "SEN"), ("Serbia", "RS", "SRB"),
        ("Seychelles", "SC", "SYC"), ("Sierra Leone", "SL", "SLE"), ("Singapore", "SG", "SGP"), ("Slovakia", "SK", "SVK"),
        ("Slovenia", "SI", "SVN"), ("Solomon Islands", "SB", "SLB"), ("Somalia", "SO", "SOM"), ("South Africa", "ZA", "ZAF"),
        ("South Sudan", "SS", "SSD"), ("Spain", "ES", "ESP"), ("Sri Lanka", "LK", "LKA"), ("Sudan", "SD", "SDN"),
        ("Suriname", "SR", "SUR"), ("Sweden", "SE", "SWE"), ("Switzerland", "CH", "CHE"), ("Syria", "SY", "SYR"),
        ("Taiwan", "TW", "TWN"), ("Tajikistan", "TJ", "TJK"), ("Tanzania", "TZ", "TZA"), ("Thailand", "TH", "THA"),
        ("Togo", "TG", "TGO"), ("Tonga", "TO", "TON"), ("Trinidad and Tobago", "TT", "TTO"), ("Tunisia", "TN", "TUN"),
        ("Turkey", "TR", "TUR"), ("Turkmenistan", "TM", "TKM"), ("Tuvalu", "TV", "TUV"), ("Uganda", "UG", "UGA"),
        ("Ukraine", "UA", "UKR"), ("United Arab Emirates", "AE", "ARE"), ("United Kingdom", "GB", "GBR"), ("United States", "US", "USA"),
        ("Uruguay", "UY", "URY"), ("Uzbekistan", "UZ", "UZB"), ("Vanuatu", "VU", "VUT"), ("Vatican City", "VA", "VAT"),
        ("Venezuela", "VE", "VEN"), ("Vietnam", "VN", "VNM"), ("Yemen", "YE", "YEM"), ("Zambia", "ZM", "ZMB"), ("Zimbabwe", "ZW", "ZWE")
    ];

    public static async Task SeedAsync(IDbContextFactory<ApplicationDbContext> dbContextFactory)
    {
        if (SeedCountries.Length == 0)
        {
            return;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var existingNames = (await dbContext.Countries.Select(c => c.Name).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingAlpha2Codes = (await dbContext.Countries.Select(c => c.IsoAlpha2Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingAlpha3Codes = (await dbContext.Countries.Select(c => c.IsoAlpha3Code).ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = new List<Country>();
        foreach (var (name, alpha2, alpha3) in SeedCountries)
        {
            // Checks against both already-committed rows AND entries already queued earlier in this same
            // pass - guards against an accidental duplicate Name/Alpha2/Alpha3 within SeedCountries itself
            // causing an unhandled unique-index violation when AddRange + SaveChangesAsync runs, which
            // would otherwise crash startup. Same pattern as AirportSeeder.
            if (!existingNames.Add(name))
            {
                continue;
            }

            if (!existingAlpha2Codes.Add(alpha2))
            {
                continue;
            }

            if (!existingAlpha3Codes.Add(alpha3))
            {
                continue;
            }

            missing.Add(new Country { Id = Guid.NewGuid(), Name = name, IsoAlpha2Code = alpha2, IsoAlpha3Code = alpha3 });
        }

        if (missing.Count == 0)
        {
            return;
        }

        dbContext.Countries.AddRange(missing);
        await dbContext.SaveChangesAsync();
    }
}
