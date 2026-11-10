namespace Core;

// Small LISP functions the generated steps call (defined once at the top of every script)
public static class LispHelpers
{
    public const string Code = """
        ;; Every object on a layer, optionally only some types e.g. "TEXT,MTEXT"
        (defun csb-objs (layer types / filter)
          (setq filter (list (cons 8 (csb-wc layer))))
          (if types (setq filter (cons (cons 0 types) filter)))
          (ssget "_X" filter))
        ;; Run fn on each object in a selection. Returns (changed skipped):
        ;; skipped counts objects AutoCAD refused, e.g. ones on a locked layer
        (defun csb-apply (ss fn / i n bad)
          (setq i 0 n 0 bad 0)
          (if ss
            (while (< i (sslength ss))
              (if (vl-catch-all-error-p
                    (vl-catch-all-apply fn (list (vlax-ename->vla-object (ssname ss i)))))
                (setq bad (1+ bad))
                (setq n (1+ n)))
              (setq i (1+ i))))
          (list n bad))
        ;; A text style by name, or nil
        (defun csb-style (name / st)
          (setq st (vl-catch-all-apply 'vla-item
                     (list (vla-get-textstyles (vla-get-activedocument (vlax-get-acad-object))) name)))
          (if (vl-catch-all-error-p st) nil st))
        ;; Change a text style's font: a font file (romans.shx, arial.ttf) or a font name (Arial).
        ;; Returns T if AutoCAD accepted it.
        (defun csb-style-font (name font / st)
          (setq st (csb-style name))
          (and st
               (not (vl-catch-all-error-p
                      (vl-catch-all-apply
                        (if (wcmatch (strcase font) "*.SHX,*.TTF,*.TTC,*.OTF")
                          (function (lambda () (vla-put-fontfile st font)))
                          (function (lambda () (vla-setfont st font :vlax-false :vlax-false 0 34))))
                        nil)))))
        ;; Change texts by handle: each edit is (handle now after). A text is only changed if it
        ;; still says "now", so anything edited by hand since the scan is left alone (skipped).
        (defun csb-settexts (edits / n bad e obj)
          (setq n 0 bad 0)
          (foreach ed edits
            (setq e (handent (car ed)))
            (if (and e
                     (entget e)
                     (setq obj (vlax-ename->vla-object e))
                     (equal (vl-catch-all-apply 'vla-get-textstring (list obj)) (cadr ed))
                     (not (vl-catch-all-error-p
                            (vl-catch-all-apply 'vla-put-textstring (list obj (caddr ed))))))
              (setq n (1+ n))
              (setq bad (1+ bad))))
          (list n bad))
        ;; Documents\CADScriptBuilder\last-run.txt, where the summary is saved for the app's history
        (defun csb-open-log ( / dir path f)
          (setq dir (strcat (getvar "MYDOCUMENTSPREFIX") "\\CADScriptBuilder"))
          (vl-mkdir dir)
          (setq path (strcat dir "\\last-run.txt"))
          (setq f (vl-catch-all-apply 'open (list path "w" "utf8")))
          (if (or (null f) (vl-catch-all-error-p f)) (setq f (open path "w")))
          f)
        ;; "12 objects" or "12 objects (3 skipped)" for the summary
        (defun csb-count (r what)
          (strcat (itoa (car r)) " " what
                  (if (> (cadr r) 0) (strcat " (" (itoa (cadr r)) " skipped)") "")))
        """;
}
