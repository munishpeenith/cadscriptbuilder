;;; 05b_without_undo_brackets.lsp
;;; Control test: the same renames as 05, run as a command, with NO undo bundle.
;;; Load it, then type CSB_RENAME to run it.
;;; Result (22 Oct 2026): one U reverted only the last rename (CSB-Z).
;;; So AutoCAD does not bundle a command's steps by itself. Every generated
;;; script keeps the UNDO BEGIN / END lines.

(command "-LAYER" "New" "CSB-X,CSB-Y,CSB-Z" "")

(defun c:CSB_RENAME ()
  (command "-RENAME" "LAyer" "CSB-X" "CSB-X2")
  (command "-RENAME" "LAyer" "CSB-Y" "CSB-Y2")
  (command "-RENAME" "LAyer" "CSB-Z" "CSB-Z2")
  (princ))

(princ)
