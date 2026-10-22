(defun c:CSB_SCAN ( / path f lay)
  (setq path (strcat (getvar "MYDOCUMENTSPREFIX") "\\repos\\cadscriptbuilder\\src\\layers.txt"))
  (setq f (open path "w"))
  (write-line (getvar "DWGNAME") f)
  (setq lay (tblnext "LAYER" T))
  (while lay
    (write-line (cdr (assoc 2 lay)) f)
    (setq lay (tblnext "LAYER")))
  (close f)
  (princ (strcat "\nCADScriptBuilder: layer list saved to " path))
  (princ))

(c:CSB_SCAN)
(princ)