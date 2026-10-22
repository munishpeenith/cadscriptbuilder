# CADScriptBuilder

Reference notes. Everything established so far about what this is, why it exists and how it behaves.
Implementation language and stack are deliberately left open.

*Last updated 22 October 2026.*

---

## 1. The problem

Engineers regularly need the same mechanical change applied in dozens of places across a live CAD
drawing. Renaming layers to a project convention. Repathing external references. Stripping out what
is unused. Easy to describe, slow to perform, and it recurs on every revision.

The options today are:

- Do it by hand, object by object.
- Use a built-in tool that does one narrow thing, if one exists for the job.
- Hunt for a script online and hope it is right.
- Write AutoLISP, which most engineers cannot.

**The gap is between knowing exactly what you want done and being able to say it in a language the
software accepts.**

### Where it came from

Scoped with Ayub Wayrah, a graduate civil engineer at AECOM, after asking what costs his team time.
His answer: he has repeatedly had to make the same bulk changes across a drawing, highly specific to
that drawing each time. On one occasion he found a script on GitHub that he could drag and drop
straight into his Civil 3D drawing, and it worked. That moment is the product.

---

## 2. Why existing tools do not close it

### LAYTRANS works, but it does one thing

The original premise was that LAYTRANS (AutoCAD's Layer Translator) fails in Civil 3D. An Autodesk
support article says it works with AutoCAD layers only, in drawings without Civil styles.

**Tested on 22 October 2026, that premise did not hold.** On a Civil 3D 2027 tutorial drawing,
LAYTRANS renamed `_EG-CONT-MJR`, the layer the surface style uses for major contours, to a new
layer. The major contours followed: turning the new layer off hid them. Not yet confirmed: whether
the change survives save, close and reopen, and whether it survives a surface rebuild.

So "LAYTRANS fails in Civil 3D" is not a justification for this project. The design brief already
sent to AECOM still makes that claim, so be ready to say it was tested and corrected. Whatever the
Autodesk article describes (a specific version, a specific layer type, a scenario not yet hit)
remains unresolved.

**What LAYTRANS genuinely cannot do:**

- **Anything beyond layer mapping.** It cannot rename, then purge what is now unused, then repath
  xrefs, then audit, as one job.
- **Dry run.** It changes the drawing; it does not report first.
- **Log.** Nothing records what it did.
- **Repeat cheaply.** Each mapping is built by hand in a dialog.

CADScriptBuilder is for the jobs that are several steps, or that need to be repeatable, reviewable
and reversible as a unit. Composition is the real argument.

### The "AutoLISP generator" GPT wrappers are unsafe

They have a model write code that then runs directly against project data, with no dry run, no undo
boundary and no guarantee the generated code resembles anything tested. The output is plausible
rather than correct.

### Lesson carried over from Invert

The previous project (a drainage schedule consistency checker) was killed after feasibility work
showed InfoDrainage already generated those schedules. Before committing to any operation, check
whether the incumbent software already does it. That habit is what caught the LAYTRANS assumption in
an afternoon rather than after weeks of building.

---

## 3. What gets generated

| Format | What it does | Decision |
| --- | --- | --- |
| **AutoLISP (`.lsp`)** | Acts on the currently open drawing | **Version 1 target** |
| **AutoCAD script (`.scr`)** | Can open drawing after drawing, so batch across a folder | Version 2 |
| **VBA (`.dvb`)** | A different mechanism entirely | Not used. Early notes wrongly conflated `.lsp` with VBA |

---

## 4. The architecture decision everything else follows from

There are two ways in:

1. **A block builder.** Drag and connect tested blocks. Scratch-like in feel, but smarter, and the
   blocks are real operations rather than toy primitives.
2. **A prompt mode.** Plain English describing the change.

**Both routes produce the same artefact: an operation plan.** A short, ordered list of named
operations and their settings. The block builder produces it by hand. Prompt mode produces it from a
sentence, validated against a schema before anything else happens.

**From the plan onwards, nothing is generated.** Each operation has a template written and tested in
advance. The engine assembles the matching templates into a file.

> The model chooses between tested components. It never writes a line of code that runs on a drawing.

### Blocks act on named things, not positions

A drawing's layers are a flat set of names, not an ordered list. There is no "between layer X and
layer Y". Every block operates on named things or sets of things (a layer, all layers matching a
pattern, all unused layers), never on a position in a sequence.

### Prompt mode shows its working

Prompt mode renders the plan it produced back to the engineer **as blocks**, so they can see what it
understood and correct it before anything is generated.

### The "generate anyway" escape hatch

When the request cannot be expressed in the operation library, the engineer can still ask for a
best-effort attempt. This is fenced off, clearly marked as untested, and requires a further explicit
confirmation. It is the one place the normal guarantee does not hold, and it says so.

---

## 5. Safety and reversibility

These are not features, they are the reason an engineer would trust the output at all.

- **Dry run.** Reports exactly what would change and changes nothing.
- **Ask before running.** A tickbox in the generator, **ticked by default**. When ticked, the
  generated script runs the dry run, then asks `Apply changes? [Yes/No]` at the command line and only
  acts on Yes. Unticking is the power-user choice.
- **A single undo group, always.** One press reverts the whole script. Not optional, even when "ask
  before running" is off.
- **A written log, always**, saved beside the drawing, recording every change made.
- **It refuses.** Anything the operation library cannot express correctly is reported, not guessed.
  This includes refusing a plan that names a layer the drawing does not contain.
- **Nothing is deleted silently.**

> A tool that is usually right is worse than one that is reliably either right, or honest about
> stopping.

---

## 6. What it deliberately does not do

- **It cannot verify what a script did to the geometry.** It does read the drawing's layer list (see
  section 8), but checking the result of a change is still on the engineer, which is why the dry run
  matters.
- **Nothing requiring geometry analysis.**
- **No batch across a folder** in version 1.
- **No model-written code reaching a live file**, with the single fenced exception above.
- **No silent side effects.** A script never changes which layer the engineer is drawing on, never
  saves on their behalf, and never touches anything outside its plan.

---

## 7. The interface

- The generator sits **in a panel beside the open drawing, not in front of it**.
- The script file is produced **inside the panel**, then **dragged and dropped onto the drawing**,
  where it runs immediately. This mirrors the GitHub-script moment that started the project.
- **Block builder on the left**, blocks drag and connect vertically into a plan.
- **Layer fields are dropdowns** of the layers that actually exist in the drawing, filled by a scan
  (section 8), with a "scanned at HH:MM" label so the engineer knows how fresh the list is. This
  makes typos in layer names impossible.
- **"Ask before running" tickbox**, ticked by default.
- **Prompt mode behind a button in the top corner**, so the deterministic path is the default and the
  model is something you reach for.
- **A Connect button** (later, section 8) to link the app to a drawing already open in AutoCAD.
- **A dry run view** showing what would change, before anything does.
- **A naming convention builder** with fields, so a convention can be defined once and reused.

---

## 8. How scripts reach AutoCAD

**Governing rule: what a script does is separate from how it gets into AutoCAD.** Every delivery
route below loads the *same* `.lsp` file. Adding a route is a new button, never a rewrite.

### Route 1: drag and drop (version 1)

The user drags the generated `.lsp` onto the drawing. Code placed outside any `defun` runs the
moment the file loads, so the script executes on drop with no extra step.

AutoCAD's `SECURELOAD` setting flags LISP files loaded from untrusted folders. The scripts folder
must be added to **Trusted Locations** (`OP` → Files → Trusted Locations). Trust the folder, not
individual files, and never trust Downloads or the Desktop.

### Route 2: Connect via COM (top priority after prompt mode)

COM is the Windows mechanism that lets one program remote-control another. Using it, the app can:

1. Find the AutoCAD that is already running.
2. List every drawing currently open in it, so Connect is a dropdown of open drawings.
3. Tell the chosen drawing to load a script.

The script runs **inside the live session**: it prints to the engineer's own command line, sees
unsaved changes, and can confirm the connection by name, for example
`CAD Script Builder connected to drawing: Profile-5C.dwg`.

- **Light on resources.** Calls take milliseconds. The only real cost is what the script itself does.
- **Refresh on events, never on a timer.** The app should not poll AutoCAD.
- **Known friction:** if AutoCAD is busy (mid-command, dialog open) the call is rejected and needs a
  wait-and-retry. COM errors arrive as cryptic hex codes. Multiple installed AutoCAD versions need
  handling.
- **Drag and drop stays** alongside Connect, as the path that needs no connection.

### Route 3: the Core Console (batch, version 2)

`accoreconsole.exe` ships with Civil 3D: a headless AutoCAD that opens a drawing, runs a script and
closes, with no window. It is the foundation for batch mode across folders.

- It reads the **saved file on disk**, not the open session, so it cannot see unsaved changes.
- It is plain AutoCAD underneath: layers, blocks and lines are readable, but Civil 3D objects
  (surfaces, alignments) appear as opaque objects.
- Rough guess of 5 to 30 seconds per drawing. **Unmeasured.** Time it on a large tutorial drawing
  before designing around it.

### Scan and sync

- A **scan script** writes the drawing's layer list to a file in the scripts folder. The app reads it
  to fill dropdowns and to refuse plans that reference missing layers.
- After the first scan, **every generated script ends by writing the current layer list back**. It
  runs inside the live drawing, so the refresh sees the true state, including unsaved changes, at no
  extra cost.

---

## 9. Naming conventions and the layer problem

Layer naming is the first real operation because it is the most common bulk change.

- **ISO 13567** is the underlying standard behind UK layer conventions.
- **BS 1192:2007 with Uniclass 1.4** produces layers like `A-G25-M_Wall`.
- **AEC (UK) with Uniclass 2015** produces layers like `A-Ef_20_05-Walls`.
- **Uniclass 2015 tables** download free from uniclass.thenbs.com. The NBS API is gated behind
  registration and is **not on the critical path**; the downloaded tables are enough.
- **AECOM use Uniclass**, but it should not be the design centre.

**Ayub's correction, which changed the design:** conventions differ **between projects**, not between
eras. Build a custom convention builder and treat published schemes as presets.

**Uniclass supplies valid target names, not mappings.** Deciding that a given layer *is* a road
centreline and therefore maps to a particular code is a judgement. Without prompt mode, the engineer
writes that mapping; with it, the model proposes it.

---

## 10. Civil 3D concepts the tool depends on

- **Object → style → layer.** Civil 3D objects (surfaces, alignments, profiles, corridors, pipe
  networks) carry a style, and the style decides which layer each component draws on. A surface's
  major contours, minor contours, border and labels can each sit on different layers.
- **Object layers vs style component layers.** Civil 3D assigns layers in two places: a default layer
  for the object itself (drawing settings) and layers named inside the style for its parts. They
  behave differently and the tool must handle both.
- **Viewports and per-viewport overrides.** Layouts (paper space) show model space through
  viewports, each with its own scale. A layer can be frozen or recoloured in one viewport and not
  another. Renames must carry those overrides with them, or layers silently appear or vanish on
  sheets.

---

## 11. Who it is for, and how far it reaches

**Primary user:** civil engineers starting out, who know exactly what change they want and have no
way to express it.

**Nothing in the engine is specific to civil engineering.** It applies whichever rules it is handed:

- Highways, structures and building services are a **configuration change**, not a rewrite.
- Another CAD platform is **a second set of templates against the same schema**.
- The reach extends to CAD users generally, and to mechanical engineers.

**It runs locally and holds no project data**, so one engineer can try it without a conversation with
IT.

---

## 12. Why it is worth building

- **Hours returned.** Bulk tidying is non-chargeable work currently done by chargeable people.
- **Consistency across a drawing set.** Faster to review and hand over, less likely to be rejected.
- **A library that compounds.** Each tested operation stays available to everyone who follows.
- **Nothing to procure.** No server, no data leaving the machine.

---

## 13. Build order

Each step works on its own, so there is something to show from step 2 onwards. The deterministic
engine is finished before any model work begins.

1. **Hand-written `.lsp` ladder** *(in progress)*
   - [x] Hello on drop
   - [x] Turn a layer off
   - [ ] Make a new layer (`-LAYER` `New`, never `Make`)
   - [ ] Rename a layer (`-RENAME`)
   - [ ] Rename three layers inside one undo, revert with a single `U`
   - [ ] Dry run, then `Apply? [Yes/No]`
   - [ ] List every layer (the first script that reads the drawing)
2. **Generator:** a plan file in, a `.lsp` out. First thing worth putting on GitHub.
3. **Second block** (delete or purge a layer), chained with rename in one plan and one undo.
4. **Scan script and drag-and-drop sync**, so layer fields become dropdowns.
5. **Block builder UI.**
6. **Prompt mode (AI).**
7. **COM Connect.** Top priority once prompt mode is in. It is the smaller job, so it can jump the
   queue if a quick visible win is needed.

**The milestone that decides the project:** one generated script, running on a real Civil 3D drawing,
doing what it said it would.

---

## 14. Testing setup

- **Machine:** Dell Latitude 5420 (Windows). Civil 3D is Windows only.
- **Software:** Civil 3D 2027, installed 22 October 2026. The trial is 30 days and cannot be
  extended; confirm the days remaining in the Autodesk account.
- **Fallback:** 2015 5K iMac (32GB) via Boot Camp with Windows 10, a 200GB partition. Only viable if
  Civil 3D 2027 still supports Windows 10 (2026 did; 2027 unverified).
- **Test drawings:** Civil 3D tutorial drawings live in
  `C:\Program Files\Autodesk\AutoCAD 2027\C3D\Help\Civil Tutorials\Drawings`, which is write-protected.
  Work on copies in Documents. Prefer drawings with surfaces and corridors.
- **Scripts folder:** `C:\Users\Munish\Documents\CADGEN`, added to Trusted Locations.
- **Editor:** VS Code with Autodesk's AutoLISP extension, which can debug against a running AutoCAD.
- **Verifying a script:** `DWGCOMPARE` against the original; check Civil styles still resolve; check
  per-viewport layer overrides survived; save, close and reopen; rebuild the surface.
- **Testers:** Munish, and Ayub against real-world drawings. **Sanitised or sample drawings only, never
  live AECOM project files.**

---

## 15. AutoLISP notes so far

- A `.lsp` is plain text. Save with **All Files** as the type in Notepad, and turn on file name
  extensions in Explorer, or it becomes `hello.lsp.txt`.
- `(command "-LAYER" "OFF" "DIFF" "")` sends keystrokes to the command line. Each string is one entry;
  `""` is Enter.
- Code outside a `defun` runs on load. `(defun c:NAME () ...)` defines a typed command instead.
- End every file with `(princ)`. Otherwise AutoCAD echoes the last expression's value (`nil`, or the
  message a second time).
- `(princ "text")` prints to the command line. It is output only; nothing is left waiting to run.
- Errors always read `; error:`. **"malformed string on input"** means unbalanced quotes.
- Turning off the *current* layer makes AutoCAD stop and ask for confirmation, which throws the
  keystroke sequence out of step.
- Undo group: `(command "_.UNDO" "_BEGIN")` ... `(command "_.UNDO" "_END")`.
- Debug by reading the command-line transcript: compare what AutoCAD asked for with what the script
  sent.
- **Learning:** Lee Mac Programming first, then AfraLISP and the CADTutor forum, with Autodesk's
  AutoLISP Reference for individual functions.

---

## 16. Open questions and risks

- **The LAYTRANS finding.** The original justification is gone. The pitch now rests on composition and
  safety, which is real but less vivid than a specific broken tool. Concrete tasks will make it
  vivid.
- **Version 1 operation library.** What changes has Ayub actually made by hand this year? That answer
  decides which blocks exist.
- **Renames and viewport overrides**, and **object layers vs style component layers**, are the two
  ways a rename can look successful and be wrong.
- **Scan freshness.** A Core Console scan sees the saved file only; Connect and self-reporting
  scripts avoid this.
- **Trust on first use.** An engineer who gets one wrong script will not run a second.
- **Testing depth.** Two testers on a limited set of drawings is thin.

---

## 17. Decisions still open

- Implementation language and runtime (which also decides how COM is reached).
- How the application is packaged and presented.
- How the operation plan is represented and serialised.
- How the scan result file is formatted.
- How templates are stored, versioned and tested.
- How the block builder is rendered and how blocks are composed.

---

## Naming and credit

**Name:** CADScriptBuilder.

**Proposed by Munish Peenith.** Scoped in conversation with Ayub Wayrah, a graduate civil engineer at
AECOM, who will also test it. Ayub is still clearing his involvement internally, so he is not named
as a co-proposer on anything sent to AECOM.
