namespace DomainModeling.Graph;

/// <summary>
/// Localized UI strings for ubiquitous language documents (Markdown, JSON API, custom exporters).
/// </summary>
public sealed class UbiquitousLanguagePhrases
{
    public required string Title { get; init; }
    public required string Introduction { get; init; }

    public required string MarkdownSectionAggregates { get; init; }
    public required string MarkdownSectionDomainEvents { get; init; }

    /// <summary>Format string with <c>{0}</c> = bounded context name (Markdown).</summary>
    public required string MarkdownBoundedContextHeadingFormat { get; init; }

    public required string NoAggregatesInContext { get; init; }
    public required string NoDomainEventsInContext { get; init; }
    public required string NoRelationsFromConcept { get; init; }

    public required string MarkdownRelationsHeading { get; init; }
    public required string MarkdownTypePrefix { get; init; }
    public required string MarkdownRelationshipViaWord { get; init; }

    public required string KindAggregate { get; init; }
    public required string KindEntity { get; init; }
    public required string KindValueObject { get; init; }
    public required string KindSubType { get; init; }

    public required string RelationshipHas { get; init; }
    public required string RelationshipHasMany { get; init; }
    public required string RelationshipContains { get; init; }
    public required string RelationshipReferences { get; init; }
    public required string RelationshipReferencesById { get; init; }

    /// <summary>
    /// Default English phrases used by <see cref="UbiquitousLanguageDefinition.CreateDefault"/>.
    /// </summary>
    public static UbiquitousLanguagePhrases English() => new()
    {
        Title = "Ubiquitous language",
        Introduction =
            "Generated from the domain model: aggregates with linked entities, value objects, and sub-types (structural relations, limited depth), then domain events — grouped by bounded context.",
        MarkdownSectionAggregates = "Aggregates",
        MarkdownSectionDomainEvents = "Domain events",
        MarkdownBoundedContextHeadingFormat = "Bounded context: {0}",
        NoAggregatesInContext = "No aggregates in this bounded context.",
        NoDomainEventsInContext = "No domain events in this bounded context.",
        NoRelationsFromConcept = "None from this concept.",
        MarkdownRelationsHeading = "Relations",
        MarkdownTypePrefix = "Type",
        MarkdownRelationshipViaWord = "via",
        KindAggregate = "aggregate",
        KindEntity = "entity",
        KindValueObject = "value object",
        KindSubType = "sub-type",
        RelationshipHas = "has",
        RelationshipHasMany = "has many",
        RelationshipContains = "contains",
        RelationshipReferences = "references",
        RelationshipReferencesById = "references by id",
    };

    internal string RelationshipPhrase(RelationshipKind kind) => kind switch
    {
        RelationshipKind.Has => RelationshipHas,
        RelationshipKind.HasMany => RelationshipHasMany,
        RelationshipKind.Contains => RelationshipContains,
        RelationshipKind.References => RelationshipReferences,
        RelationshipKind.ReferencesById => RelationshipReferencesById,
        _ => kind.ToString(),
    };

    internal string KindLabelFor(string internalKind) => internalKind switch
    {
        "aggregate" => KindAggregate,
        "entity" => KindEntity,
        "value object" => KindValueObject,
        "sub-type" => KindSubType,
        _ => internalKind,
    };
}

/// <summary>
/// Defines localized phrase sets for ubiquitous language output. Hosts can replace the default or add languages.
/// </summary>
public sealed class UbiquitousLanguageDefinition
{
    /// <summary>BCP 47 or short key (e.g. <c>en</c>, <c>nl</c>) used when no language is requested.</summary>
    public required string DefaultLanguage { get; init; }

    /// <summary>Phrase set per language key.</summary>
    public required IReadOnlyDictionary<string, UbiquitousLanguagePhrases> Languages { get; init; }

    /// <summary>Maximum hops from an aggregate root into linked concepts.</summary>
    public int MaxConceptDepth { get; init; } = 4;

    /// <summary>
    /// Default definition (English only), used when hosts do not call <see cref="DomainModelOptions.UseUbiquitousLanguage"/>.
    /// </summary>
    public static UbiquitousLanguageDefinition CreateDefault() => new()
    {
        DefaultLanguage = "en",
        Languages = new Dictionary<string, UbiquitousLanguagePhrases>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = UbiquitousLanguagePhrases.English(),
        },
    };

    internal UbiquitousLanguagePhrases ResolvePhrases(string? requestedLanguage)
    {
        var key = string.IsNullOrWhiteSpace(requestedLanguage) ? DefaultLanguage : requestedLanguage.Trim();
        if (Languages.TryGetValue(key, out var direct))
            return direct;

        foreach (var kv in Languages)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase))
                return kv.Value;
        }

        return Languages.TryGetValue(DefaultLanguage, out var fallback)
            ? fallback
            : Languages.Values.First();
    }

    internal IReadOnlyList<string> LanguageKeys => Languages.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>
/// Fluent builder for <see cref="UbiquitousLanguageDefinition"/> — customize defaults and add translations.
/// </summary>
public sealed class UbiquitousLanguageDefinitionBuilder
{
    private string _defaultLanguage = "en";
    private int _maxDepth = 4;
    private readonly Dictionary<string, UbiquitousLanguagePhrases> _languages = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Starts a new builder (English is added automatically in <see cref="Build"/> if no languages were registered).</summary>
    public static UbiquitousLanguageDefinitionBuilder Create() => new();

    /// <summary>
    /// Sets the language key returned when clients omit <c>?lang=</c> (must exist in <see cref="Language"/> registrations).
    /// </summary>
    public UbiquitousLanguageDefinitionBuilder UseDefaultLanguage(string languageKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageKey);
        _defaultLanguage = languageKey.Trim();
        return this;
    }

    /// <summary>
    /// Overrides maximum nesting depth from aggregate roots (default 4).
    /// </summary>
    public UbiquitousLanguageDefinitionBuilder WithMaxConceptDepth(int depth)
    {
        if (depth < 1)
            throw new ArgumentOutOfRangeException(nameof(depth), "Depth must be at least 1.");
        _maxDepth = depth;
        return this;
    }

    /// <summary>
    /// Registers or replaces phrases for a language. New languages start from a copy of English unless
    /// <paramref name="cloneFrom"/> names another existing key to use as a template.
    /// </summary>
    public UbiquitousLanguageDefinitionBuilder Language(
        string languageKey,
        Action<UbiquitousLanguagePhrasesBuilder> configure,
        string? cloneFrom = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(languageKey);
        ArgumentNullException.ThrowIfNull(configure);

        var templateKey = string.IsNullOrWhiteSpace(cloneFrom) ? "en" : cloneFrom!;
        if (!_languages.TryGetValue(templateKey, out var template))
            template = UbiquitousLanguagePhrases.English();

        var pb = new UbiquitousLanguagePhrasesBuilder(template);
        configure(pb);
        _languages[languageKey.Trim()] = pb.Build();
        return this;
    }

    /// <summary>
    /// Builds the definition. Ensures <see cref="UbiquitousLanguageDefinition.DefaultLanguage"/> has a phrase set
    /// (seeds <c>en</c> from <see cref="UbiquitousLanguagePhrases.English"/> when missing).
    /// </summary>
    public UbiquitousLanguageDefinition Build()
    {
        if (!_languages.ContainsKey("en") &&
            !_languages.Keys.Any(k => string.Equals(k, "en", StringComparison.OrdinalIgnoreCase)))
        {
            _languages["en"] = UbiquitousLanguagePhrases.English();
        }

        if (!_languages.ContainsKey(_defaultLanguage) &&
            !_languages.Keys.Any(k => string.Equals(k, _defaultLanguage, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Ubiquitous language default language '{_defaultLanguage}' has no phrase set. Call Language(\"{_defaultLanguage}\", ...) or UseDefaultLanguage with an existing key.");
        }

        var normalizedDefault = _languages.Keys.First(k =>
            string.Equals(k, _defaultLanguage, StringComparison.OrdinalIgnoreCase));

        return new UbiquitousLanguageDefinition
        {
            DefaultLanguage = normalizedDefault,
            Languages = new Dictionary<string, UbiquitousLanguagePhrases>(_languages, StringComparer.OrdinalIgnoreCase),
            MaxConceptDepth = _maxDepth,
        };
    }
}

/// <summary>
/// Mutable builder for one <see cref="UbiquitousLanguagePhrases"/> instance.
/// </summary>
public sealed class UbiquitousLanguagePhrasesBuilder(UbiquitousLanguagePhrases copyFrom)
{
    private string _title = copyFrom.Title;
    private string _introduction = copyFrom.Introduction;
    private string _mdAgg = copyFrom.MarkdownSectionAggregates;
    private string _mdEv = copyFrom.MarkdownSectionDomainEvents;
    private string _mdBc = copyFrom.MarkdownBoundedContextHeadingFormat;
    private string _noAgg = copyFrom.NoAggregatesInContext;
    private string _noEv = copyFrom.NoDomainEventsInContext;
    private string _noRel = copyFrom.NoRelationsFromConcept;
    private string _mdRel = copyFrom.MarkdownRelationsHeading;
    private string _mdType = copyFrom.MarkdownTypePrefix;
    private string _mdVia = copyFrom.MarkdownRelationshipViaWord;
    private string _kAgg = copyFrom.KindAggregate;
    private string _kEnt = copyFrom.KindEntity;
    private string _kVo = copyFrom.KindValueObject;
    private string _kSt = copyFrom.KindSubType;
    private string _rHas = copyFrom.RelationshipHas;
    private string _rMany = copyFrom.RelationshipHasMany;
    private string _rCon = copyFrom.RelationshipContains;
    private string _rRef = copyFrom.RelationshipReferences;
    private string _rId = copyFrom.RelationshipReferencesById;

    public UbiquitousLanguagePhrasesBuilder WithTitle(string value)
    {
        _title = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithIntroduction(string value)
    {
        _introduction = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithMarkdownSectionAggregates(string value)
    {
        _mdAgg = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithMarkdownSectionDomainEvents(string value)
    {
        _mdEv = value;
        return this;
    }

    /// <summary>Format with <c>{0}</c> replaced by bounded context name for Markdown.</summary>
    public UbiquitousLanguagePhrasesBuilder WithMarkdownBoundedContextHeadingFormat(string value)
    {
        _mdBc = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithNoAggregatesInContext(string value)
    {
        _noAgg = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithNoDomainEventsInContext(string value)
    {
        _noEv = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithNoRelationsFromConcept(string value)
    {
        _noRel = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithMarkdownRelationsHeading(string value)
    {
        _mdRel = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithMarkdownTypePrefix(string value)
    {
        _mdType = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithMarkdownRelationshipViaWord(string value)
    {
        _mdVia = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithKindAggregate(string value)
    {
        _kAgg = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithKindEntity(string value)
    {
        _kEnt = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithKindValueObject(string value)
    {
        _kVo = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithKindSubType(string value)
    {
        _kSt = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithRelationshipHas(string value)
    {
        _rHas = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithRelationshipHasMany(string value)
    {
        _rMany = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithRelationshipContains(string value)
    {
        _rCon = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithRelationshipReferences(string value)
    {
        _rRef = value;
        return this;
    }

    public UbiquitousLanguagePhrasesBuilder WithRelationshipReferencesById(string value)
    {
        _rId = value;
        return this;
    }

    public UbiquitousLanguagePhrases Build() => new()
    {
        Title = _title,
        Introduction = _introduction,
        MarkdownSectionAggregates = _mdAgg,
        MarkdownSectionDomainEvents = _mdEv,
        MarkdownBoundedContextHeadingFormat = _mdBc,
        NoAggregatesInContext = _noAgg,
        NoDomainEventsInContext = _noEv,
        NoRelationsFromConcept = _noRel,
        MarkdownRelationsHeading = _mdRel,
        MarkdownTypePrefix = _mdType,
        MarkdownRelationshipViaWord = _mdVia,
        KindAggregate = _kAgg,
        KindEntity = _kEnt,
        KindValueObject = _kVo,
        KindSubType = _kSt,
        RelationshipHas = _rHas,
        RelationshipHasMany = _rMany,
        RelationshipContains = _rCon,
        RelationshipReferences = _rRef,
        RelationshipReferencesById = _rId,
    };
}
