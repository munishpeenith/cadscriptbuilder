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
        ;; "12 objects" or "12 objects (3 skipped)" for the summary
        (defun csb-count (r what)
          (strcat (itoa (car r)) " " what
                  (if (> (cadr r) 0) (strcat " (" (itoa (cadr r)) " skipped)") "")))
        """;
}
