# DecisionEngine/ — release-decision specifications

Decides whether a candidate release should be grabbed and (later) imported.

## Pattern

The classic specification pattern. Each rule implements
`IDecisionEngineSpecification`:

```csharp
DownloadSpecDecision IsSatisfiedBy(RemoteBook subject, SearchCriteriaBase searchCriteria);
```

The runner (`DownloadDecisionMaker`, ~288 LoC) iterates every registered
specification. If any rule rejects, the release is rejected with a structured
reason. The same pattern is reused for imports
(`../MediaFiles/BookImport/Specifications/`).

## Notable specifications

`AcceptableSizeSpecification`, `BlocklistSpecification`,
`CustomFormatAllowedByProfileSpecification`,
`DelayProfileSpecification`, `LanguageSpecification`,
`MonitoredAuthorSpecification`, `NoMissingOrUnmonitoredSpecification`,
`QualityAllowedByProfileSpecification`,
`UpgradableSpecification`.

## Auto-registration

Specifications are discovered by the DryIoc `AutoAddServices` scan; no
manual `Register<T>` call is needed. Adding a new spec is just dropping a
file in this directory.

## Specs vs handlers

`IHandle<TEvent>` runs *after* a decision (reactive). Specifications run
*before* (gating). Don't confuse the two when designing new rules.
