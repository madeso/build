import os
import subprocess
import solution

def toGraphviz(source, target, format, exclude, simplify, style, reverseArrows):
    """
    public
    :type source: string
    :type target: string
    :type format: string
    :type exclude: list[string]
    :type: simplify: bool
    :type style: string
    :type reverseArrows: bool
    """
    my_target = target or ""
    if my_target.strip() == "" or my_target.strip() == "?":
        my_target = os.path.splitext(source)[0]
    my_format = format or ""
    if my_format.strip() == "" or my_format.strip() == "?":
        my_format = "svg"
    logic(source, exclude or [], my_target, my_format, simplify, style or "dot", reverseArrows)

def getFile(source, target, format):
    """
    internal
    :rtype: string
    :type source: string
    :type target: string
    :type format: string
    """
    my_target = target
    if my_target.strip() == "" or my_target.strip() == "?":
        my_target = os.path.splitext(source)[0]
    if os.path.isdir(my_target):
        my_target = os.path.join(my_target, os.path.splitext(os.path.basename(source))[0])
    my_format = format
    if my_format.strip() == "" or my_format.strip() == "?":
        my_format = "svg"
    f = my_target + "." + my_format
    return f

def GetProjects(source):
    """
    public
    :rtype: IEnumerable<string>
    :type source: string
    """
    s = solution.Solution(source, [], False, False)
    for p in s.projects:
        yield p.Value.Name

def logic(solutionFilePath, exlude, targetFile, format, simplify, style, reverseArrows):
    """
    private
    :type solutionFilePath: string
    :type exlude: List<string>
    :type targetFile: string
    :type format: string
    :type simplify: bool
    :type style: string
    :type reverseArrows: bool
    """
    s = solution.Solution(solutionFilePath, exlude, simplify, reverseArrows)
    s.writeGraphviz(targetFile + ".graphviz")
    graphviz(targetFile, format, style)

def graphviz(targetFile, format, style):
    """
    private
    :type targetFile: string
    :type format: string
    :type style: string
    """
    cmdline = ["dot", targetFile+".graphviz", "-T" + format, "-K" + style, "-O" + targetFile + "." + format]
    print("Running graphviz ", cmdline)
    s = subprocess.call(cmdline)
