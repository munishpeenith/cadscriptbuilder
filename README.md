# CADScriptBuilder

A tool that lets engineers build custom AutoCAD and Civil 3D automations from tested blocks, with a
dry run, a single undo and a log on every change. AI-assisted planning comes later; the engine comes
first.

*Last updated 27 October 2026.*

---

## Current status

**Working now (tested in Civil 3D 2027):** a Windows app, a command line generator, and 17 layer
blocks.

- **The app** (`src/Gui`, WPF with a Windows 11 look):
  - Drag the SCAN tile into a drawing; the app syncs with it and shows *Connected: drawing.dwg*.
  - Build steps from 17 layer blocks, plus *Rename by pattern* (find and replace across many layers).
  - Dropdowns list only the layers that make sense for the block (Thaw shows frozen layers, and so on).
  - Select several layers at once; steps are added in the order you tick them.
  - Every step is checked against the drawing as it will be after the steps before it:
    ✔ fine, ⚠ heads up (runs, but changes nothing), ✖ can't work (held back, *Add anyway* if meant).
  - Drag steps to reorder, Delete to remove, double-click to edit, Clear all.
  - Save plans and open them on other drawings; change drawing without losing your steps.
  - *Generate* makes a RUN tile you drag straight into Civil 3D.
  - Pin to keep it on top of Civil 3D; snaps side by side.
- **Every generated script:** previews each change, asks `Apply changes? [Yes/No]` (No by default),
  bundles everything into one undo, then prints a summary of what was done, including any purge
  that didn't happen because the layer still had objects on it.
- **The command line** (`src/Generator`) runs saved plans with the same checks.

**Next:** AI mode, then a full Scratch-style block builder.

---

## Running it

From the repo root:

```
dotnet run --project src/Gui                         # the app
dotnet run --project src/Generator -- plans/rename-and-purge.json out/run.lsp $HOME/Documents/CADScriptBuilder/layers.txt
```

To build a standalone `.exe` to share (no .NET needed on the other PC):

```
dotnet publish src/Gui -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

Keep `CADScriptBuilder.exe` and `csb_scan.lsp` together. The app is unsigned, so Windows shows
*More info → Run anyway* the first time.

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

The original premise was that LAYTRANS (AutoCAD's Layer Translator) fails in Civil 3D, based on an
Autodesk support article saying it works only in drawings without Civil styles.

**Tested on 22 October 2026, that premise did not hold.** On a Civil 3D 2027 tutorial drawing,
LAYTRANS renamed `_EG-CONT-MJR`, the layer a surface style uses for major contours, and the contours
followed it. Not yet confirmed: whether that survives save, reopen and a surface rebuild. The design
brief sent to AECOM still makes the original claim, so be ready to say it was tested and corrected.

**What LAYTRANS genuinely cannot do:** chain operations (rename, then purge, then repath, as one
job), dry-run, log, or repeat cheaply without rebuilding the mapping in a dialog. CADScriptBuilder is
for jobs that are several steps, or that need to be repeatable, reviewable and reversible as a unit.
Composition is the real argument.

### The "AutoLISP generator" GPT wrappers are unsafe

They have a model write code that runs directly against project data, with no dry run, no undo
boundary and no guarantee the code resembles anything tested.

### Lesson carried over from Invert

The previous project was killed after feasibility work showed the incumbent software already did the
job. Check before building. That habit caught the LAYTRANS assumption in an afternoon.

---

## 3. What gets generated

| Format | What it does | Decision |
| --- | --- | --- |
| **AutoLISP (`.lsp`)** | Acts on the currently open drawing | **Version 1 target** |
| **AutoCAD script (`.scr`)** | Can open drawing after drawing, so batch across a folder | Version 2 |
| **VBA (`.dvb`)** | A different mechanism entirely | Not used |

---

## 4. The architecture

### The plan is the centre

There are two ways in:

1. **A block builder.** Drag and connect tested blocks.
2. **A prompt mode.** Plain English describing the change.

**Both produce the same thing: an operation plan.** A short, ordered list of named operations and
their settings. The plan says *what* to do and knows nothing about *how* it will be carried out.

**From the plan onwards, nothing is generated.** Each operation has a template written and tested in
advance, and the engine assembles them into a file.

> The model chooses between tested components. It never writes a line of code that runs on a drawing.

Because the plan is independent of execution, a second way of running plans (such as an AutoCAD
plugin, section 8) can be added later without rewriting anything.

### Blocks act on named things, not positions

A drawing's layers are a flat set of names, not an ordered list. Every block operates on named things
or sets of things, never on a position.

### Prompt mode shows its working

Prompt mode renders the plan it produced back as blocks, so the engineer can correct it before
anything is generated.

### The "generate anyway" escape hatch

For requests the library cannot express, a best-effort attempt is available, fenced off, marked
untested, and gated behind an extra confirmation.

---

## 5. Safety and reversibility

- **Dry run.** Reports exactly what would change and changes nothing.
- **Ask before running.** A tickbox in the generator, **ticked by default**. Ticked, the script runs
  the dry run, then asks `Apply changes? [Yes/No] <No>` and only acts on Yes.
- **A single undo group, always.** Proven necessary: without `UNDO BEGIN`/`END`, one `U` reverts only
  the last change (see `lsp/05b`).
- **A confirmation message, always,** naming what changed and telling the engineer one `U` reverses
  it. AutoCAD cannot give an undo step a custom name, so this message is how the change is labelled.
- **A written log, always**, saved beside the drawing.
- **It refuses** anything the library cannot express, including plans that name a layer the drawing
  does not contain.
- **Nothing is deleted silently.**

> A tool that is usually right is worse than one that is reliably either right, or honest about
> stopping.

---

## 6. What it deliberately does not do

- **It cannot verify what a script did to the geometry.** It reads the layer list (section 8) but
  checking results is still on the engineer, which is why the dry run matters.
- **No geometry analysis.**
- **No batch across a folder** in version 1.
- **No model-written code reaching a live file**, except the fenced escape hatch.
- **No silent side effects.** A script never changes the engineer's current layer, never saves on
  their behalf, never touches anything outside its plan.

---

## 7. The interface

- A panel **beside the open drawing, not in front of it**.
- The script is produced in the panel and **dragged onto the drawing**, where it runs immediately.
- **Block builder on the left.**
- **Layer fields are dropdowns** of real layers, filled from the scan, with a "scanned at" time.
- **"Ask before running" tickbox**, ticked by default.
- **Prompt mode behind a button in the top corner.**
- **A Connect button** later, to link to a drawing already open in AutoCAD.
- **A naming convention builder**, so a convention is defined once and reused.

The look is still open. Modern web-style UIs are reachable from C# by hosting web content inside a
desktop window.

---

## 8. How scripts reach AutoCAD

**Governing rule: what a script does is separate from how it gets into AutoCAD.**

### Route 1: drag and drop (version 1)

Code outside any `defun` runs the moment the file loads, so scripts execute on drop. The scripts
folder must be in AutoCAD's **Trusted Locations** (`OP` → Files) or `SECURELOAD` flags every load.

### Route 2: Connect via COM (after prompt mode)

The app finds the running AutoCAD, lists open drawings, and tells the chosen one to load a script.
The script runs inside the live session, sees unsaved changes, and prints to the engineer's own
command line. Light on resources. Known friction: "AutoCAD is busy" rejections needing a retry, and
cryptic error codes. Drag and drop stays alongside it.

### Route 3: the Core Console (batch, version 2)

`accoreconsole.exe` ships with Civil 3D: a headless AutoCAD for running scripts on drawings that are
not open. Reads the saved file only, and sees Civil 3D objects as opaque. Speed unmeasured.

### Parked: an AutoCAD plugin

A .NET plugin runs inside AutoCAD, edits the drawing directly, has full Civil 3D access, built-in
transaction rollback, and could dock the panel inside AutoCAD. It is also what click-to-select
interaction would need. **Decided against for now:** it needs installing (possibly IT approval at a
large firm), every code change means restarting AutoCAD, and it ties the tool to AutoCAD versions.
Because the plan is independent (section 4), it can be added later as a second way of running plans.

### Scan and sync

- `lsp/07_scrape_layers_to_write.lsp` (copied next to the app as `csb_scan.lsp`) writes the drawing
  name, then every layer, to `Documents\CADScriptBuilder\layers.txt`. Lines starting with `*` carry
  extra detail (layer names can't contain `*`): `*scan=3`, `*current=`, `*linetype=`, `*frozen=`,
  `*off=`, `*locked=`, `*noplot=`. Older scans still load; checks that need missing detail are skipped.
- The app watches that file and ticks green the moment a new scan lands.
- Scans live outside the repo, so layer names from real drawings never reach GitHub.

---

## 9. Naming conventions

- **ISO 13567** underlies UK layer conventions.
- **BS 1192:2007 with Uniclass 1.4** produces layers like `A-G25-M_Wall`.
- **AEC (UK) with Uniclass 2015** produces layers like `A-Ef_20_05-Walls`.
- **Uniclass 2015 tables** download free from uniclass.thenbs.com. The NBS API is gated and not on the
  critical path.

**Ayub's correction:** conventions differ **between projects**. Build a custom convention builder and
treat published schemes as presets.

**Uniclass supplies valid target names, not mappings.** Deciding what a layer *is* is a judgement;
without prompt mode the engineer writes the mapping, with it the model proposes one.

---

## 10. Civil 3D concepts the tool depends on

- **Object → style → layer.** Civil 3D objects carry a style, and the style decides which layer each
  component draws on.
- **Object layers vs style component layers.** Two different places Civil 3D assigns layers. Both
  must be handled.
- **Viewport overrides.** A layer can be frozen or recoloured per viewport. Renames must carry those
  overrides, or layers silently appear or vanish on sheets.

---

## 11. Who it is for

Civil engineers starting out, and CAD users generally. Nothing in the engine is civil-specific:
other disciplines are a configuration change, other CAD platforms a second template set. It runs
locally and holds no project data.

---

## 12. Why it is worth building

- **Hours returned** on non-chargeable bulk tidying.
- **Consistency** across a drawing set.
- **A library that compounds**, so the tenth project costs almost nothing.
- **Nothing to procure.**

---

## 13. Build order

1. **Hand-written `.lsp` ladder** *(done)*
   - [x] `01` Hello on drop
   - [x] `02` Turn a layer off
   - [x] `03` Make a new layer
   - [x] `04` Rename a layer
   - [x] `05` Three renames in one undo (`05b`: control test proving the undo brackets are needed)
   - [x] `06` Dry run, then `Apply? [Yes/No]`
   - [x] `07` Scan the layer list to a file
2. **Generator** *(done)*: a C# console app. Plan file in, `.lsp` out.
3. **Second block** *(done)*: purge, chained with rename in one plan and one undo.
4. **Sync** *(done)*: plans checked against the scan; nothing generated if they don't match.
5. **App** *(done)*: scan page, steps page, 17 layer blocks, smart checks, save/open plans.
6. **Prompt mode (AI)** *(next)*.
7. **Full Scratch-style block builder.**
8. **COM Connect.** Smaller job than 6, so it can jump the queue for a quick win.

**The milestone that decides the project:** one *generated* script, on a real Civil 3D drawing, doing
what it said it would.

---

## 14. Decisions

**Made:**

| Decision | Choice | Why |
| --- | --- | --- |
| Generator language | **C# on .NET 10** | Clean Windows `.exe`, native COM, and the only route to a plugin later |
| AutoCAD plugin | **Parked** | Install friction, slow dev loop, version tie-in |
| Scan file format | **Plain text**, drawing name then one layer per line | Simplest for any reader |
| Undo naming | **Not possible**; use a printed confirmation | AutoCAD labels bundles as `GROUP` |
| Yes/No prompt | **`getkword`**, No as default | Reflex Enter or Space must be the safe key |

| Plan file format | **JSON** | Structured, read natively by C#, and what blocks and AI will produce |
| UI | **WPF + WPF-UI** (Windows 11 Fluent look) | Native drag and drop, single-file `.exe` |
| Checks | **Problems block, notes don't** | Never generate a broken script, but don't nag about harmless steps |

**Still open:**

- How templates are stored, versioned and tested.
- Which operations make the version 1 library. The question for Ayub: what changes has he actually
  made by hand this year?

---

## 15. Repo layout

```
README.md         this file
lsp/              hand-written AutoLISP, numbered in build order (07 is the scan script)
plans/            example plans (JSON)
src/Core/         the engine: plans, scans, checks, blocks (Operations/), LISP building
src/Generator/    command line: plan + scan in, .lsp out
src/Gui/          the Windows app
```

---

## 16. Testing setup

- **Machine:** Dell Latitude 5420, Windows. Civil 3D is Windows only.
- **Software:** Civil 3D 2027, installed 22 October 2026. 30-day trial; check days remaining in the
  Autodesk account.
- **Test drawings:** copies of the tutorial drawings from
  `C:\Program Files\Autodesk\AutoCAD 2027\C3D\Help\Civil Tutorials\Drawings` (the originals are
  write-protected).
- **Editor:** VS Code with Autodesk's AutoLISP extension and the C# Dev Kit.
- **Verifying a script:** `DWGCOMPARE`; check Civil styles still resolve; check viewport overrides;
  save, close and reopen; rebuild the surface.
- **Testers:** Munish, and Ayub. **Sample or sanitised drawings only, never live project files.**

---

## 17. AutoLISP notes

- A `.lsp` is plain text. Save as **All Files** in Notepad and turn on file extensions in Explorer.
- `(command "-LAYER" "OFF" "DIFF" "")` sends keystrokes. Each string is one entry; `""` is Enter.
- Capitals in AutoCAD prompts (`LAyer`) mark the shortest accepted shortcut.
- Code outside a `defun` runs on load. `(defun c:NAME () ...)` only defines a command; type its name,
  or call `(c:NAME)`, to run it.
- End every file with `(princ)` to stop AutoCAD echoing a stray `nil`.
- `princ` is output only. Nothing is left waiting to run.
- Errors always read `; error:`. **"malformed string on input"** means unbalanced quotes.
- **Every command goes on the undo stack**, including harmless ones like listing layers.
- `-LAYER` `New` creates without switching to the layer. `Make` switches. Use `New`.
- Turning off the current layer triggers a confirmation that throws the keystrokes out of step.
- `(initget "Yes No")` then `getkword` asks a restricted question. `if` with `progn` branches on it.
- `tblnext` walks a table (e.g. layers) one entry at a time; `tblsearch` checks one exists.
- Each table entry is a list of labelled values; label `2` is the name: `(cdr (assoc 2 entry))`.
- Files: `open` (with `"w"` to replace), `write-line`, then always `close`.
- `MYDOCUMENTSPREFIX` gives the Documents path on any machine. Backslashes in strings are written `\\`.
- Debug by reading the command-line transcript: what AutoCAD asked for against what the script sent.

---

## 18. Learning

- **AutoLISP:** Lee Mac Programming, AfraLISP, the CADTutor forum, Autodesk's AutoLISP Reference.
- **C#:** W3Schools for basics, Microsoft Learn for the free official courses. Priorities for the
  generator: types, `if` and loops, classes and lists, reading and writing files, building strings,
  reading JSON.

---

## Credit

**Proposed by Munish Peenith.** Scoped in conversation with Ayub Wayrah, a graduate civil engineer at
AECOM, who also tests it. Ayub is still clearing his involvement internally, so he is not named as a
co-proposer on anything sent to AECOM.
