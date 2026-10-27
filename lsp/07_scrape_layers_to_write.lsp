;; Scan v3: writes the drawing's layers to layers.txt, plus extra info lines
;; starting with * (layer names can't contain *):
;;   *scan=      the scan file version
;;   *current=   the current layer
;;   *linetype=  each loaded linetype
;;   *frozen=    each frozen layer
;;   *off=       each layer that's turned off
;;   *locked=    each locked layer
;;   *noplot=    each layer set not to plot
(defun c:CSB_SCAN ( / path f lay name flags ent)
  (vl-mkdir (strcat (getvar "MYDOCUMENTSPREFIX") "\\CADScriptBuilder"))
  (setq path (strcat (getvar "MYDOCUMENTSPREFIX") "\\CADScriptBuilder\\layers.txt"))
  (setq f (open path "w"))
  (write-line (getvar "DWGNAME") f)
  (write-line "*scan=3" f)
  (write-line (strcat "*current=" (getvar "CLAYER")) f)
  (setq lay (tblnext "LTYPE" T))
  (while lay
    (write-line (strcat "*linetype=" (cdr (assoc 2 lay))) f)
    (setq lay (tblnext "LTYPE")))
  (setq lay (tblnext "LAYER" T))
  (while lay
    (setq name (cdr (assoc 2 lay))
          flags (cdr (assoc 70 lay)))
    (write-line name f)
    (if (= 1 (logand 1 flags))
      (write-line (strcat "*frozen=" name) f))
    (if (= 4 (logand 4 flags))
      (write-line (strcat "*locked=" name) f))
    ;; a negative colour number means the layer is off
    (if (minusp (cdr (assoc 62 lay)))
      (write-line (strcat "*off=" name) f))
    ;; the plot setting isn't in tblnext, so read the layer's full record
    (setq ent (entget (tblobjname "LAYER" name)))
    (if (equal (cdr (assoc 290 ent)) 0)
      (write-line (strcat "*noplot=" name) f))
    (setq lay (tblnext "LAYER")))
  (close f)
  (princ (strcat "\nCADScriptBuilder: layer list saved to " path))
  (princ))

(c:CSB_SCAN)
(princ)
