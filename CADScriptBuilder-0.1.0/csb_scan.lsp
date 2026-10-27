;; Scan v2: writes the drawing's layers to layers.txt, plus extra info lines
;; starting with * (layer names can't contain *):
;;   *current=   the current layer
;;   *linetype=  each loaded linetype
;;   *frozen=    each frozen layer
(defun c:CSB_SCAN ( / path f lay name)
  (vl-mkdir (strcat (getvar "MYDOCUMENTSPREFIX") "\\CADScriptBuilder"))
  (setq path (strcat (getvar "MYDOCUMENTSPREFIX") "\\CADScriptBuilder\\layers.txt"))
  (setq f (open path "w"))
  (write-line (getvar "DWGNAME") f)
  (write-line (strcat "*current=" (getvar "CLAYER")) f)
  (setq lay (tblnext "LTYPE" T))
  (while lay
    (write-line (strcat "*linetype=" (cdr (assoc 2 lay))) f)
    (setq lay (tblnext "LTYPE")))
  (setq lay (tblnext "LAYER" T))
  (while lay
    (setq name (cdr (assoc 2 lay)))
    (write-line name f)
    (if (= 1 (logand 1 (cdr (assoc 70 lay))))
      (write-line (strcat "*frozen=" name) f))
    (setq lay (tblnext "LAYER")))
  (close f)
  (princ (strcat "\nCADScriptBuilder: layer list saved to " path))
  (princ))

(c:CSB_SCAN)
(princ)
