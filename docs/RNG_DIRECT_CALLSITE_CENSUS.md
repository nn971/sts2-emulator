# RNG direct-call source inventory (emulator-rng-callsite-source-inventory-v1)

Direct qualified calls to PrototypeRng, NativeRngV111, and NativeFisherYatesV111 in Sts2Emulator.Core. This does not include indirect delegates, random provider interfaces, third-party/native source callsites, or prove that every source operation is mapped.

Direct call expressions: **36**.

Counts by direct API family:

- `legacy_prototype_api`: 3
- `native_primitive_api`: 8
- `native_shuffle_mechanics`: 25

Rows distinguish legacy prototype calls, native primitive calls, generic shuffle-helper calls, and recognized provider/fixture support. Unrecognized contexts still need manual source tracing.

| File:line | API | Operation | Source expression | Review status |
| --- | --- | --- | --- | --- |
| [`src/Sts2Emulator.Core/NativeDarvEventV111.cs:163`](../src/Sts2Emulator.Core/NativeDarvEventV111.cs#L163) | `NativeFisherYatesV111` | `ShuffleInPlace` | `decisions.AddRange(NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/NativeDarvEventV111.cs:252`](../src/Sts2Emulator.Core/NativeDarvEventV111.cs#L252) | `NativeFisherYatesV111` | `ShuffleInPlace` | `_ = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/NativeEventContextRngV111.cs:170`](../src/Sts2Emulator.Core/NativeEventContextRngV111.cs#L170) | `NativeFisherYatesV111` | `ShuffleInPlace` | `_ = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/NativeGloryAncientsV111.cs:252`](../src/Sts2Emulator.Core/NativeGloryAncientsV111.cs#L252) | `NativeFisherYatesV111` | `ShuffleInPlace` | `_ = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/NativeGloryAncientsV111.cs:293`](../src/Sts2Emulator.Core/NativeGloryAncientsV111.cs#L293) | `NativeFisherYatesV111` | `ShuffleInPlace` | `_ = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/NativeNeowEventV111.cs:45`](../src/Sts2Emulator.Core/NativeNeowEventV111.cs#L45) | `NativeFisherYatesV111` | `ShuffleInPlace` | `decisions.AddRange(NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/NativeNonupeipeEventV111.cs:89`](../src/Sts2Emulator.Core/NativeNonupeipeEventV111.cs#L89) | `NativeFisherYatesV111` | `ShuffleInPlace` | `var choices = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/NativeNonupeipeEventV111.cs:151`](../src/Sts2Emulator.Core/NativeNonupeipeEventV111.cs#L151) | `NativeFisherYatesV111` | `ShuffleInPlace` | `_ = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/NativeRunRngV111.cs:92`](../src/Sts2Emulator.Core/NativeRunRngV111.cs#L92) | `NativeRngV111` | `NextInt` | `value = NativeRngV111.NextInt(advanced, streamId, maxExclusive);` | `provider_or_codec_infrastructure` |
| [`src/Sts2Emulator.Core/NativeShuffleSourceImportV111.cs:165`](../src/Sts2Emulator.Core/NativeShuffleSourceImportV111.cs#L165) | `NativeRngV111` | `NextInt` | `int expected = NativeRngV111.NextInt(rng, "run.shuffle", bound);` | `provider_or_codec_infrastructure` |
| [`src/Sts2Emulator.Core/NativeShuffleStepReplayV111.cs:141`](../src/Sts2Emulator.Core/NativeShuffleStepReplayV111.cs#L141) | `PrototypeRng` | `Shuffle` | `PrototypeRng.Shuffle(engineRng, "combat", output);` | `fixture_or_replay_support` |
| [`src/Sts2Emulator.Core/NativeShuffleStepReplayV111.cs:148`](../src/Sts2Emulator.Core/NativeShuffleStepReplayV111.cs#L148) | `PrototypeRng` | `Shuffle` | `PrototypeRng.Shuffle(engineRng, "combat", unchanged);` | `fixture_or_replay_support` |
| [`src/Sts2Emulator.Core/NativeShuffleStepReplayV111.cs:173`](../src/Sts2Emulator.Core/NativeShuffleStepReplayV111.cs#L173) | `NativeFisherYatesV111` | `Execute` | `var computed = NativeFisherYatesV111.Execute(` | `fixture_or_replay_support` |
| [`src/Sts2Emulator.Core/NativeTanxEventV111.cs:104`](../src/Sts2Emulator.Core/NativeTanxEventV111.cs#L104) | `NativeFisherYatesV111` | `ShuffleInPlace` | `var choices = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/NativeTanxEventV111.cs:169`](../src/Sts2Emulator.Core/NativeTanxEventV111.cs#L169) | `NativeFisherYatesV111` | `ShuffleInPlace` | `_ = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/NativeVakuuEventV111.cs:95`](../src/Sts2Emulator.Core/NativeVakuuEventV111.cs#L95) | `NativeFisherYatesV111` | `ShuffleInPlace` | `choices.AddRange(NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/NativeVakuuEventV111.cs:165`](../src/Sts2Emulator.Core/NativeVakuuEventV111.cs#L165) | `NativeFisherYatesV111` | `ShuffleInPlace` | `var choices = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/PrototypeCombat.cs:372`](../src/Sts2Emulator.Core/PrototypeCombat.cs#L372) | `NativeFisherYatesV111` | `ShuffleInPlace` | `_ = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/PrototypeCombat.cs:7932`](../src/Sts2Emulator.Core/PrototypeCombat.cs#L7932) | `NativeFisherYatesV111` | `Execute` | `var shuffle = NativeFisherYatesV111.Execute(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/PrototypeCombat.cs:13827`](../src/Sts2Emulator.Core/PrototypeCombat.cs#L13827) | `NativeFisherYatesV111` | `ShuffleInPlace` | `_ = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/PrototypeCombat.cs:16128`](../src/Sts2Emulator.Core/PrototypeCombat.cs#L16128) | `NativeFisherYatesV111` | `Execute` | `var shuffle = NativeFisherYatesV111.Execute(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/PrototypeNativeGloryRepy.cs:149`](../src/Sts2Emulator.Core/PrototypeNativeGloryRepy.cs#L149) | `NativeFisherYatesV111` | `ShuffleInPlace` | `_ = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/PrototypeNativeLaterActMap.cs:115`](../src/Sts2Emulator.Core/PrototypeNativeLaterActMap.cs#L115) | `NativeFisherYatesV111` | `ShuffleInPlace` | `var choices = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/PrototypeNativeLavaRock.cs:63`](../src/Sts2Emulator.Core/PrototypeNativeLavaRock.cs#L63) | `NativeFisherYatesV111` | `ShuffleInPlace` | `_ = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/PrototypeNativeOvergrowthMap.cs:74`](../src/Sts2Emulator.Core/PrototypeNativeOvergrowthMap.cs#L74) | `NativeFisherYatesV111` | `ShuffleInPlace` | `var choices = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/PrototypeNativeRelicGrabBag.cs:145`](../src/Sts2Emulator.Core/PrototypeNativeRelicGrabBag.cs#L145) | `NativeFisherYatesV111` | `ShuffleInPlace` | `_ = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/PrototypeNativeWarHammer.cs:42`](../src/Sts2Emulator.Core/PrototypeNativeWarHammer.cs#L42) | `NativeFisherYatesV111` | `ShuffleInPlace` | `_ = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/PrototypeRooms.cs:4814`](../src/Sts2Emulator.Core/PrototypeRooms.cs#L4814) | `NativeFisherYatesV111` | `ShuffleInPlace` | `_ = NativeFisherYatesV111.ShuffleInPlace(` | `typed_provider_shuffle_mechanics_boundary` |
| [`src/Sts2Emulator.Core/RandomGaussianDoubleProvidersV111.cs:84`](../src/Sts2Emulator.Core/RandomGaussianDoubleProvidersV111.cs#L84) | `NativeRngV111` | `NextGaussianDouble` | `var value = NativeRngV111.NextGaussianDouble(` | `provider_or_codec_infrastructure` |
| [`src/Sts2Emulator.Core/RandomGaussianIntProvidersV111.cs:79`](../src/Sts2Emulator.Core/RandomGaussianIntProvidersV111.cs#L79) | `NativeRngV111` | `NextGaussianInt` | `var value = NativeRngV111.NextGaussianInt(` | `provider_or_codec_infrastructure` |
| [`src/Sts2Emulator.Core/RandomIndexProvidersV111.cs:97`](../src/Sts2Emulator.Core/RandomIndexProvidersV111.cs#L97) | `NativeRngV111` | `NextInt` | `int index = NativeRngV111.NextInt(streams, request.StreamId, request.SupportCount);` | `provider_or_codec_infrastructure` |
| [`src/Sts2Emulator.Core/RandomIndexProvidersV111.cs:135`](../src/Sts2Emulator.Core/RandomIndexProvidersV111.cs#L135) | `PrototypeRng` | `NextInt` | `var selectedIndex = PrototypeRng.NextInt(` | `provider_or_codec_infrastructure` |
| [`src/Sts2Emulator.Core/RandomIndexProvidersV111.cs:297`](../src/Sts2Emulator.Core/RandomIndexProvidersV111.cs#L297) | `NativeFisherYatesV111` | `ShuffleInPlace` | `return NativeFisherYatesV111.ShuffleInPlace(` | `provider_or_codec_infrastructure` |
| [`src/Sts2Emulator.Core/RandomRangedDoubleProvidersV111.cs:79`](../src/Sts2Emulator.Core/RandomRangedDoubleProvidersV111.cs#L79) | `NativeRngV111` | `NextDouble` | `var value = NativeRngV111.NextDouble(` | `provider_or_codec_infrastructure` |
| [`src/Sts2Emulator.Core/RandomRangedFloatProvidersV111.cs:79`](../src/Sts2Emulator.Core/RandomRangedFloatProvidersV111.cs#L79) | `NativeRngV111` | `NextFloat` | `var value = NativeRngV111.NextFloat(` | `provider_or_codec_infrastructure` |
| [`src/Sts2Emulator.Core/RandomUnitFloatProvidersV111.cs:77`](../src/Sts2Emulator.Core/RandomUnitFloatProvidersV111.cs#L77) | `NativeRngV111` | `NextFloat` | `var value = NativeRngV111.NextFloat(streams, request.StreamId);` | `provider_or_codec_infrastructure` |
