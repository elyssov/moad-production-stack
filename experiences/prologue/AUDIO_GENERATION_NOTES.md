# MOAD Prologue - Audio Generation Notes

The prologue may use Fable or another API-driven generation route to produce
new voiceover audio. Existing `assets/voice/*.mp3` files are useful for timing,
prototype playback, and comparison, but a Fable/HuggingFace pass should be
treated as a new casting and performance attempt until accepted by the owner.

## Source Text

Generate from:

`text_directed/*.md`

Each file contains:

- speaker;
- director notes;
- scene emotion;
- voiceover text.

Do not generate from the visual-caption page splits unless the target is a
timed caption prototype. Voiceover should use the full directed take text.

## Model Search Route

If Fable can search or call HuggingFace audio models through an API, use that as
a model-selection helper, not as an excuse to accept the first usable voice.

Search for separate voice/TTS candidates for:

- Alice / Lady Alice Compton;
- Eugene / Lord Eugene Compton;
- Together / final paired line workflow.

The model route should be logged with:

- model name;
- license / usage terms;
- voice style description;
- language/accent capability;
- whether it supports long-form narration;
- whether it supports emotional control;
- whether it supports SSML, pauses, or prompt-based delivery direction;
- output format and sample rate.

## Audition Process

Do not batch the whole prologue first. Audition short tests:

Alice:

- `04_alice_wwi_triage.md`, one paragraph from the moral-injury section.
- `07_alice_call_in_the_night.md`, the "Then I woke screaming" passage.
- `13c_alice_we_are_going_in.md`, full line.

Eugene:

- `03_eugene_wwi_assault.md`, one war paragraph.
- `08a_eugene_black_stone_night.md`, one grief/procedure paragraph.
- `13d_eugene_for_our_boy.md`, full line.

Together:

- `13e_together_god_help_anything.md`, full line, both as unison and as two
  overlapped separate takes if the tool allows it.

Only after the voice direction passes should the full text set be generated.

## Acceptance Gate

Alice passes only if she sounds:

- adult;
- English aristocratic;
- warm, intelligent, and intimate;
- emotionally alive without becoming melodramatic;
- strong enough to face death, war, and the occult.

Reject Alice if she sounds:

- childlike;
- breathy/helpless;
- generic fantasy witch;
- modern influencer;
- over-sobbing or theatrical.

Eugene passes only if he sounds:

- adult, 40+;
- English aristocratic;
- low, firm, and controlled;
- officer-like under pressure;
- emotionally restrained but not dead.

Reject Eugene if he sounds:

- young;
- villain-gravel;
- generic epic king;
- heavy regional caricature;
- shouty action trailer narrator.

Together passes only if the final line sounds like a married vow from two
people who are prepared to walk into hell for their son. It must not sound like
a marketing tagline.

## Delivery Notes For Generation Prompts

Use plain, direct generation prompts. Avoid overloaded literary prose in model
prompts; the text itself already carries the writing.

Alice prompt seed:

`Adult English aristocratic woman, 1930s, warm intelligent diary narration, cultivated pronunciation, intimate but controlled, grief and fear held under discipline, not modern, not breathy, not melodramatic.`

Eugene prompt seed:

`Adult English aristocratic man, 40s, deep controlled baritone, Great War officer, precise diction, restrained grief, command presence, old money without parody, not a villain, not a fantasy king.`

Together prompt seed:

`Married English aristocratic couple, intimate and resolved, calm vow before entering danger, Alice warmer and Eugene lower/harder, controlled rather than shouted.`

## File Output Convention

If new Fable/HuggingFace audio is generated, place accepted or reviewable takes
under a dated attempt folder, for example:

`docs/prologue/assets/voice/fable_YYYY-MM-DD_attempt_01/`

Use the same base names as `text_directed/*.md`:

- `01_eugene_valley_of_the_kings.mp3`
- `02_alice_africa_mbambwe.mp3`
- ...
- `13e_together_god_help_anything.mp3`

Keep rejected auditions in a sibling `rejected/` folder only if they are useful
for explaining what failed. Otherwise do not bloat the repository with bad
audio attempts.

