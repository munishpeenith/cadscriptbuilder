;;; 02_layer_off.lsp
;;; Turns the DIFF layer off.
;;; Proves: a script can drive AutoCAD commands, one string per keystroke, "" for Enter.

(command "-LAYER" "OFF" "DIFF" "")
(princ)
