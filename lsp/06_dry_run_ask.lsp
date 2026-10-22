;;; 06_dry_run_ask.lsp
;;; Dry run, then ask before changing anything.
;;; Prints what it would do, asks Apply changes? [Yes/No], and only acts on Yes.
;;; Pressing Enter with no answer counts as No: the safe choice is the default.
;;; On Yes, the rename runs inside one undo bundle and confirms what it did.
;;; This is the pattern every generated script will follow.

(defun c:CSB_DIFF ()
  (princ "\nWould rename DIFF -> DIFF2")
  (initget "Yes No")
  (if (= (getkword "\nApply changes? [Yes/No] <No>: ") "Yes")
    (progn
      (command "_.UNDO" "_BEGIN")
      (command "-RENAME" "LAyer" "DIFF" "DIFF2")
      (command "_.UNDO" "_END")
      (princ "\nCADScriptBuilder: renamed DIFF -> DIFF2. Type U once to undo."))
    (princ "\nCancelled. Nothing changed."))
  (princ))

(c:CSB_DIFF)
(princ)
