**ONE MORE THING — research and next design pass**

Research date: 21 September 2026. This is a design proposal, not a record of implemented changes. It draws on the original design document, the current prototype and the sources linked below. No gameplay changes were made during this research pass.

Driving question: How can game mechanics communicate the experience of competing priorities and attention fragmentation associated with ADHD?

The proposed answer is to let players form a clear intention, encounter another understandable demand, make satisfying local progress, and then experience the effort of returning to their earlier intention while time has moved on. The intended emotional progression is competence, attraction, unfinished obligation, reorientation and recognition. A difficult countdown alone would provide weak evidence for this answer.

**What the evidence supports**

| Evidence | Finding relevant to this project | Boundary on the claim |
| --- | --- | --- |
| [Gerling et al., 2024: ADHD representation in games](https://publikationen.bibliothek.kit.edu/1000172118/157946368) | Interviews with 15 teenagers and subsequent concept feedback emphasised varied experiences, coping, and the distinction between ADHD and a universally stressful setting. | These were game concepts and a small adolescent sample; they do not validate this adult morning scenario or demonstrate that playing produces empathy. |
| [Fuermaier et al., 2013: complex prospective memory](https://journals.plos.org/plosone/article?id=10.1371/journal.pone.0058338) | In a comparison of 45 adults with ADHD and 45 matched participants, difficulties were pronounced in planning and switching; plan recall showed a negligible group difference. | This particular task and sample do not justify making a character randomly forget everything. Switching here concerns performance within a structured test, not a tendency to perform more observable switches. |
| [Ek and Isaksson, 2013: everyday activities](https://pubmed.ncbi.nlm.nih.gov/23713791/?dopt=Abstract) | Interviews with 12 adults described engagement in relation to inspiration, support and social connection. | Qualitative insight into possible motivations, not a universal account or a numerical design prescription. |
| [Dodhia and Dismukes: interruptions and prospective memory](https://onlinelibrary.wiley.com/doi/10.1002/acp.1441) | Two experiments support treating the return to an interrupted task as remembering a future intention; encoding and retrieval conditions matter. | General cognition research, not ADHD-specific evidence. Useful for designing and measuring resumption. |
| [Chiossi et al., CHI 2023: short-form video and intentions](https://arxiv.org/abs/2302.03714) | In an experiment with 60 participants, the TikTok condition impaired prospective-memory performance; the other tested conditions did not show the same effect. | Does not establish that all screen use has the same effect or that the result is specific to ADHD. |
| [Barkley, Murphy and Bush, 2001: time estimation and reproduction](https://pubmed.ncbi.nlm.nih.gov/11499990/) | Adults with ADHD differed on some time-reproduction measures. The time-estimation group difference became nonsignificant after controlling for IQ. | No support for a universal faster subjective clock or a clinically accurate 2x multiplier. Our accelerated countdown is an expressive abstraction. |
| [Bowen and Horlin, 2026: hyperfocus interviews](https://journals.sagepub.com/doi/10.1177/27546330261469762) | Participants described interest and difficulties leaving an absorbed state. | Exploratory interviews with seven ADHD-identifying adults, four formally diagnosed. Useful texture, not sufficient evidence for a universal hyperfocus mechanic. |
| [NIMH: adult ADHD](https://www.nimh.nih.gov/health/publications/adhd-what-you-need-to-know) | Attention, organisation, task completion and time management can be affected; presentations vary. | Ordinary distraction during a short game cannot establish that a player has experienced ADHD itself. |

The design decisions below are project-specific hypotheses to test. The studies do not prescribe these mechanics, timings or interfaces.

**What the current prototype can already communicate**

The house, required departure tasks, linked chores, thought queue and shared clock provide enough material for the next study. Laundry and dishes create unfinished intentions across locations. The phone can introduce social demands. Picking up a book or switching a light can make the house feel responsive without another objective system.

The main risks are interpretive:

- Very short holds plus large deductions can look like a fee for helping around the house. The deduction needs to read as compressed activity time.
- A screen that immediately accelerates time when crossed by the camera can look like a hazard the player should avoid looking at.
- Optional messages explicitly described as unnecessary can teach a simple correct/incorrect choice. They need credible reasons to matter.
- Sixty movable props can become a toy-physics attraction. Adding more will not necessarily deepen the research question.
- The current `Switches` counter increments when work moves to a different task ID, including useful steps within one chore. The `Interruptions` counter concerns leaving an unfinished hold. Neither directly measures a forgotten intention or the experience of fragmentation across a whole chore.

**Recommended bounded pass**

Keep one house, the five-minute run, three departure requirements, the existing two chore chains and existing device pages. Implement four small work packages in order. The first two form the next playable iteration; the last two make its interpretation and evaluation stronger.

| Order | Work package | Reuse | Completion criterion |
| --- | --- | --- | --- |
| 1 | Make chore progress and time costs legible | Existing holds, prerequisites, props, completion events and HUD | Players can explain what changed, what remains, and why the clock moved. |
| 2 | Author one convincing interruption-and-return sequence | Existing phone messages and thought queue | Players encounter a plausible competing demand during an unfinished chain, with quiet space before and after. |
| 3 | Add one modest support: pin a next step | Existing Tab recall and task data | A player can preserve one intention without stopping time or completing it automatically. |
| 4 | Improve the end summary and activity logging | Existing CSV events and results screen | The summary distinguishes progress, activity changes, browsing time and unfinished work without asserting motives. |

**1. Feedback that makes actions feel good and consequences understandable**

| Moment | Proposed feedback | Intended feeling |
| --- | --- | --- |
| Collect a laundry pile | Cloth rustle, pile disappears, brief “Laundry collected: 1/2 — upstairs bathroom still to do.” | I achieved something; there is a clear next step. |
| Return to a locked washer | “Need the upstairs bathroom laundry.” Once both piles are collected: “Hold E — start washer.” | The interruption has a concrete unfinished consequence. |
| Wash dishes | Water loop during the existing hold; remove existing dish meshes in a few stages; preserve progress when released. | Competence and satisfying completion. |
| Finish a costly action | One brief clock movement and “Wash finished · 20 seconds passed.” Preserve the advance cost preview. | The task was useful, and doing it took time. |
| Look at a screen | Test a roughly 0.5-second sustained look before 2x begins; use a stable indicator and a single subtle onset cue. | Attention has settled here; this was not an accidental camera penalty. |
| Close a device | A brief statement of game-clock time spent in that visit, using actual logged time including completion costs. | I did not notice how long that took. |
| Use a loose prop or switch | A relevant handling sound or switch click plus a short action prompt. No new task card. | The house is tempting and responsive. |

Those durations are starting values for playtesting. Do not add another automatic time charge when returning to a task: travel, re-reading and decisions already take time. Keep movement, aiming and input responsive.

Keep the requested colours and no legend. Action words carry the meaning: “Collect,” “Use,” “Pick up,” and a specific missing prerequisite. Colour supplements those words. Pair a phone sound with a small visible message cue. This follows the principle of communicating critical information through more than colour or a single sensory channel in [Xbox Accessibility Guideline 103](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/103).

**2. One sequence with competing reasons to act**

An example using the existing house:

1. The player wakes with a clear departure goal and collects their phone. Give the opening some quiet space.
2. They encounter laundry in the upstairs bathroom. Starting a wash before leaving seems useful, and collecting this pile feels quick.
3. After that collection, one phone message becomes eligible: “Are you bringing the notes? They're probably still in your bag.” The phone must still be owned before any message arrives.
4. The message connects to departure preparations, so checking it is understandable. The same device also contains the existing feed and cat clips. The player may leave immediately or continue browsing.
5. When they later approach the washer, its incomplete state cues the earlier laundry intention. A specific remaining step is available, and previous work is intact.
6. Completing the wash provides a small moment of relief; the shared clock preserves the conflict with leaving.

This sequence is an authored possibility, not a forced route. A player who declines the phone or leaves successfully must still be allowed to do so. The protagonist's need to leave should be clear from the start.

Use at most three authored notification beats for this pass, rather than increasing the number of messages. Give each a reason and a distinct context. Allow only one fresh attention cue at a time and test a short cooldown between cues. Preserve quiet stretches. Avoid stacking a message, a thought animation, an audio alert and a large clock flash simultaneously.

The thought queue can make the newest concern prominent and let older concerns slip from the small display. The underlying task state and Tab recall stay reliable. Avoid randomly deleting an item, lying about progress, moving the camera for the player, or preventing them from closing the phone. The prototype cannot reproduce the full difficulty of disengaging simply by offering an exit button; test whether its content creates a felt pull and report that limitation honestly.

**3. A small support that is part of play**

Let the player pin one next step from the existing recall view: “Get downstairs laundry.” New concerns can still appear, but that one intention remains accessible. This needs one selection state and a marker in an existing UI, not an inventory app or a new resource meter.

Offer it without a time fee, a score penalty or a “cure” label. It supports remembering; it cannot do the chore, recover spent time or decide priorities. For the next research iteration, compare this version with the same scene without the pin. If that additional UI threatens the schedule, first study how players already use Tab recall, and defer pinning.

**4. A summary that helps the player recognise a pattern**

Keep the existing outcome but replace moral labels with observable state: “Completed,” “Started, unfinished,” and “Not started.” A message that never arrived should not be called “ignored”; an uncompleted task should not automatically be called “forgotten.” Credit collection progress even when its parent chore is unfinished.

Show a short sequence from actual events, for example: “Collected upstairs laundry → checked phone → packed bag → returned to laundry.” Include two or three useful totals, such as device time, chores started versus finished, and departure requirements completed. Do not turn every activity change into a failure statistic.

For logging, group the two laundry pickups and washer under one activity, and the three dish collections and washing under another. Keep individual task events for debugging. Add device-open, page-change and close events so browsing is visible; retain separate real and game-clock time. Do not infer attention from a hover, or forgetting from a delay. Attribute a return interval to the unfinished activity only as an observation, then ask what the player intended.

**Playtest that can answer the driving question**

Use a small formative sample, for example six to eight volunteers, including several people with ADHD who want to comment on representation. This can reveal design problems and contrasting accounts; it cannot establish a universal experience or a statistically reliable empathy effect. Treat disagreement as useful data.

For the first run, explain controls and the goal of leaving. Do not hide the success rules to manufacture confusion. Avoid revealing the interpretation you hope to hear, within the testing consent/process that applies to the project. Let people play quietly; continuous think-aloud could itself interrupt them. Ask about particular moments afterward.

Ask these before naming a target emotion:

1. What were you trying to do at that point?
2. What made the next activity feel worth doing?
3. Was there something you meant to return to? What reminded you?
4. When the clock changed, what did you think caused it?
5. What helped you keep track, and what made that harder?
6. What do you think the game was communicating?

Then ask participants with relevant lived experience which moments felt familiar, which felt artificial, and what was missing. Do not require personal diagnostic details beyond what they choose to share.

| Design claim | Evidence to look for | Reason to revise |
| --- | --- | --- |
| Priorities compete | Player can describe two worthwhile intentions and why one displaced the other. | Player only describes obvious good tasks and obvious traps. |
| Attention becomes fragmented | Event sequence and retrospective account identify an unfinished intention and a diversion. | Task changes are mostly successful sequential chore steps or basic navigation problems. |
| Time slips during engagement | Player reports a mismatch between expected and elapsed time while understanding the clock's behaviour. | Player thinks looking around triggers an unexplained punishment. |
| Feedback supports competence | Player knows what was collected, what is missing and whether progress persisted. | Confusion comes from unclear colliders, prompts or progress states. |
| The support helps | Player uses recall/pinning and can explain its value or limitations. | The aid either goes unnoticed or completes the prioritisation problem for them. |

For a small comparison, change one feature at a time. Start with feedback clarity; later compare the pinned-step support. Alternate version order where feasible and acknowledge that replay teaches the house layout. Do not use higher failure rates or more switches as automatic evidence of better representation.

**Scope boundary for this iteration**

No additional rooms, required chores, NPC simulation, full phone operating system, new minigame, complex carrying inventory, random task scrambling, fatigue/dopamine meters, forced camera motion or constant alarm soundtrack. Keep the miscellaneous physics props already present, but give them no extra UI objectives or punishment system. They can remain an ambient temptation while the two chore chains do the explanatory work.

The research target is a player account such as: “I knew I needed to leave, but each thing I did seemed sensible, and getting back to my earlier plan took effort.” That is a proposed evaluation criterion, not an observed playtest result.
