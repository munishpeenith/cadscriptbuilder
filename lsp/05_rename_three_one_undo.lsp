;;; 05_rename_three_one_undo.lsp
;;; Creates three test layers, then renames all three inside one undo bundle.
;;; Proves: a single U reverts all three renames together.
;;; The test layers are created outside the bundle, so they survive the undo.

(command "-LAYER" "New" "CSB-A,CSB-B,CSB-C" "")
(command "_.UNDO" "_BEGIN")
(command "-RENAME" "LAyer" "CSB-A" "CSB-A2")
(command "-RENAME" "LAyer" "CSB-B" "CSB-B2")
(command "-RENAME" "LAyer" "CSB-C" "CSB-C2")
(command "_.UNDO" "_END")
(princ)
