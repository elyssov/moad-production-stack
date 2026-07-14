# MOAD Prologue - Fable Video Brief

Use this folder as the full source packet:

- Stills: `assets/images/`
- Writing surfaces: `assets/backgrounds/`
- Directed text: `text_directed/`
- Voice refs/finals: `assets/voice/`
- Sound refs/finals: `assets/sfx/`
- Playback reference: `index.html`
- Audio-generation notes: `AUDIO_GENERATION_NOTES.md`

## Goal

Create a cinematic prologue video from the approved still frames and the
approved diary/typewriter text presentation. The video should feel like an
occult-pulp aristocratic tragedy, not a trailer montage and not a modern
explainer.

The still frames are canon. Do not invent new characters, escorts, convoys,
locations, or alternate plot beats.

## Hard Canon Constraints

- The Black Stone abduction happens in the Egyptian Gallery / museum at Compton
  Hall, not in a tomb.
- Alexander is the Comptons' son; the parents fail to reach him before the
  Stone closes.
- Eugene and Alice go to Egypt alone. No hired expedition party accompanies
  them into the final approach.
- The half-track is their desperate transport, not a military convoy.
- Eugene is an English lord and Great War officer, 40+, heavy and dangerous.
- Alice is an English aristocratic woman, brilliant, warm, strong-willed, and
  carrying Mbambwe's occult teaching.
- Do not sanitize war, triage, death, child peril, occult horror, or military
  violence. These are story pillars.

## Structure

Use the image order from `README.md`.

For each beat:

1. Start with the full still image.
2. Hold long enough to read the scene.
3. Add only subtle motion: slow push, small parallax, dust, firelight, smoke,
   paper texture, typewriter key motion, or candle/flicker.
4. Transition to the writing surface for the narration.
5. Show Alice text on the Moleskine page.
6. Show Eugene text on the Underwood/typewriter frame.

If the generator struggles with character animation, prefer restrained camera
motion over facial/body warping. Preserve the stills.

## Visual Style

Overall:

- 1930s occult pulp, British aristocratic tragedy, early expedition cinema.
- Rich painterly stills; do not make them look like flat stock photos.
- Slow, controlled cuts. Let the horror breathe.
- Avoid fast trailer rhythm.

Alice Moleskine:

- Private diary intimacy.
- Warm paper, handwritten text, pen movement, occasional page turn.
- Her writing can feel alive and emotional, but the page must remain readable.

Eugene Underwood:

- Hard mechanical typewriter presentation.
- Darker, heavier, more percussive than Alice.
- Typebar/key sounds, platen movement, paper feed, ink bite.
- Text should appear as typed strikes, not a soft fade.

Final together lines:

- Use both voices or two visual languages in tension.
- If only one background can be used, use the darker Underwood setup and let
  Alice's line cut through first with warmer tone.

## Voice Direction

Use the per-take director notes in `text_directed/*.md`. The summary below is
the global casting and performance rule.

Fable or another API route may generate new voiceover audio. Treat the current
`assets/voice/*.mp3` files as timing/prototype/reference unless the owner
explicitly accepts them as final. Follow `AUDIO_GENERATION_NOTES.md` for
HuggingFace/model-search auditions and file output rules.

### Alice / Lady Alice Compton

Adult female English aristocrat. Warm, intelligent, intimate, and composed.
She is not a modern casual narrator and not a breathy helpless heroine.

Voice:

- Cultivated English pronunciation.
- Clear, elegant consonants.
- Slightly more lyrical and fluid than Eugene.
- Warm lower-mid register preferred over girlish brightness.
- Emotion is allowed, but she was raised to control herself.

Performance:

- Diary intimacy: she writes for herself, not for an audience.
- In Africa: curious, alive, young in memory, but narrated by adult Alice.
- In triage: controlled horror, fatigue, moral injury.
- In the hospital/magic beat: wonder, terror, love beginning as certainty.
- In the abduction: maternal panic under aristocratic discipline; when she
  says she woke screaming, do not make the entire take a scream.
- At the tomb: resolved, pale, intimate, ready to enter hell for her son.

Avoid:

- Generic fantasy witch voice.
- Modern influencer warmth.
- Over-sobbing.
- Childlike pitch.

### Eugene / Lord Eugene Compton

Adult male English aristocrat and Great War officer. Deep baritone or low
resonant voice. He speaks like a man trained to command under fire.

Voice:

- Cultivated English, precise consonants.
- Low, firm, restrained.
- Old money without parody.
- Not a heavy Scots accent. He served in the Royal Scots Fusiliers; he is not
  being played as a regional caricature.

Performance:

- Family/history passages: dry pride, education, memory, restraint.
- War passages: controlled violence, no bragging, no shame, exactness.
- Alice/hospital passages: love and disbelief under masculine restraint.
- Black Stone passages: grief compressed into procedure and resolve.
- Tomb entrance: dangerous calm, a father checking his weapon before entering
  the impossible.

Avoid:

- Shouting except where the text truly demands force.
- Generic epic king delivery.
- Villain gravel.
- Melodramatic sobbing.

### Together

They are a married couple, not two disconnected narrators. The final lines need
shared history.

Delivery:

- Alice first: warmer, intimate, decisive.
- Eugene second: lower, harder, protective.
- Final together line: controlled vow, not a battle cry.

## Sound Direction

Use sound as texture, not as constant noise.

- Desert: wind, sand, distant tools, dry cloth.
- Africa: insects, low drums, fire, night.
- Great War: distant artillery, gas, mud, stretcher lines, not nonstop chaos.
- Hospital: muffled voices, canvas rain, breath, heartbeat only where needed.
- Compton Hall: fireplace, clock, large sleeping house.
- Black Stone: low pressure, almost sub-audible pull, silence after closure.
- Library/map: paper, page turn, pen, early morning quiet.
- Aircraft: propeller and route-map transition.
- Desert half-track: engine heat, wind, radiator, canvas, metal.
- Tomb: stone, wind drop, low rumble.

## Fable Failure Rules

If Fable cannot handle a beat cleanly:

- Do not accept distorted faces.
- Do not accept extra people or modern props.
- Do not accept changed weapons or convoy logic.
- Do not accept the Stone moving to the tomb.
- Do not accept Alexander aging up or turning into a generic child.
- Prefer a still shot with clean camera movement over broken animation.

The output can be a video exploration, but the approved stills and text remain
canon until the owner explicitly accepts a new render.
