namespace Core;

// The scan, in one place. The SCAN tile runs it, and every generated script runs it again
// at the end, so the app always re-syncs with the real drawing.
// It writes Documents\CADScriptBuilder\layers.txt: the drawing name, then one layer per line,
// plus extra info lines starting with * (layer names can't contain *).
public static class ScanLisp
{
    // Helpers every script needs. csb-wc makes a name safe inside an ssget filter,
    // where characters like # @ . * ? , would otherwise act as wildcards.
    public const string Helpers = """
        (vl-load-com)
        (defun csb-wc (s / out c i)
          (setq out "" i 1)
          (while (<= i (strlen s))
            (setq c (substr s i 1))
            (if (member c '("#" "@" "." "*" "?" "~" "[" "]" "-" "," "`"))
              (setq out (strcat out "`" c))
              (setq out (strcat out c)))
            (setq i (1+ i)))
          out)
        """;

    // csb-scan: source is nil for a normal scan, or "run" when a script calls it after running
    public const string Function = """
        (defun csb-scan (source / dir path f lay name ss i e ed obj txt lyr)
          (setq dir (strcat (getvar "MYDOCUMENTSPREFIX") "\\CADScriptBuilder"))
          (vl-mkdir dir)
          (setq path (strcat dir "\\layers.txt"))
          ;; UTF-8 keeps symbols like the degree sign intact (older AutoCAD falls back to plain open)
          (setq f (vl-catch-all-apply 'open (list path "w" "utf8")))
          (if (or (null f) (vl-catch-all-error-p f)) (setq f (open path "w")))
          (if f
            (progn
              (write-line (getvar "DWGNAME") f)
              (write-line "*scan=4" f)
              (if source (write-line (strcat "*source=" source) f))
              (write-line (strcat "*current=" (getvar "CLAYER")) f)
              (write-line (strcat "*ltscale=" (rtos (getvar "LTSCALE") 2 6)) f)
              (write-line (strcat "*celtscale=" (rtos (getvar "CELTSCALE") 2 6)) f)
              (write-line (strcat "*psltscale=" (itoa (getvar "PSLTSCALE"))) f)
              (write-line (strcat "*msltscale=" (itoa (getvar "MSLTSCALE"))) f)
              (setq lay (tblnext "LTYPE" T))
              (while lay
                (write-line (strcat "*linetype=" (cdr (assoc 2 lay))) f)
                (setq lay (tblnext "LTYPE")))
              (setq lay (tblnext "LAYER" T))
              (while lay
                (setq name (cdr (assoc 2 lay)))
                (write-line name f)
                (if (= 1 (logand 1 (cdr (assoc 70 lay)))) (write-line (strcat "*frozen=" name) f))
                (if (= 4 (logand 4 (cdr (assoc 70 lay)))) (write-line (strcat "*locked=" name) f))
                (if (minusp (cdr (assoc 62 lay))) (write-line (strcat "*off=" name) f))
                (if (equal (cdr (assoc 290 (entget (tblobjname "LAYER" name)))) 0)
                  (write-line (strcat "*noplot=" name) f))
                (setq ss (ssget "_X" (list (cons 8 (csb-wc name)))))
                (write-line (strcat "*count=" (itoa (if ss (sslength ss) 0)) "|" name) f)
                (setq lay (tblnext "LAYER")))
              ;; text styles (flag 1 means a shape file, not a text style)
              (setq lay (tblnext "STYLE" T))
              (while lay
                (setq name (cdr (assoc 2 lay)))
                (if (and (/= name "") (= 0 (logand 1 (cdr (assoc 70 lay)))))
                  (write-line (strcat "*textstyle=" (rtos (cdr (assoc 40 lay)) 2 6) "|"
                                      (cdr (assoc 3 lay)) "|" name) f))
                (setq lay (tblnext "STYLE")))
              ;; every piece of text: type|handle|length of layer name|layer name, then the text
              (setq ss (ssget "_X" '((0 . "TEXT,MTEXT,MULTILEADER"))) i 0)
              (if ss
                (while (< i (sslength ss))
                  (setq e (ssname ss i) ed (entget e) obj (vlax-ename->vla-object e)
                        txt (vl-catch-all-apply 'vla-get-textstring (list obj)))
                  (if (= (type txt) 'STR)
                    (progn
                      (setq lyr (cdr (assoc 8 ed))
                            txt (vl-string-translate "\r\n\t" "   " txt))
                      (write-line (strcat "*text=" (cdr (assoc 0 ed)) "|" (cdr (assoc 5 ed)) "|"
                                          (itoa (strlen lyr)) "|" lyr txt) f)))
                  (setq i (1+ i))))
              (close f)))
          (princ))
        """;

    // The script behind the SCAN tile
    public static string Script =>
        ";; CADScriptBuilder scan: saves this drawing's layers, styles and text for the app\n" +
        Helpers + "\n" + Function + "\n" +
        "(csb-scan nil)\n" +
        "(princ \"\\nCADScriptBuilder: drawing scanned. Switch back to the app.\")\n" +
        "(princ)\n";
}
