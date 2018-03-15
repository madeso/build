from subprocess import call
import os
from _winreg import *

def Run(mywd, mycmd):
	try:
		retcode = call(mycmd, cwd=mywd, shell=True)
		if retcode < 0:
			print >>sys.stderr, "Child was terminated by signal", -retcode
		#else ifmyspeak:
		#	print >>sys.stderr, "Child returned", retcode
	except OSError, e:
		print "Execution failed:", e



cwd = os.getcwd()
drives = ["C", "D", "W"]
plattforms = ["x86", "amd64"]
conf = ["debug", "release"]
wd = os.path.join(cwd, "libxml2\\win32")

print "Current dir: ", wd

############################################# find all the installed studios
VCVARS = "..\\..\\VC\\vcvarsall.bat" # need to go 2 folders back rom the "install dir"
studios = []
print "Looking for installed visual studios..."
aReg = ConnectRegistry(None,HKEY_LOCAL_MACHINE)
aKey = OpenKey(aReg, r"SOFTWARE\Microsoft\VisualStudio")
for i in range(1024):
	try:
		keyname = EnumKey(aKey, i)
		# print keyname
		asubkey = OpenKey(aKey, keyname)
		val = QueryValueEx(asubkey, "InstallDir")[0]
		v = os.path.expandvars(os.path.join(val, VCVARS)) # expandvars expand the back folder .. 
		# print "Investigating ", v
		if os.path.isfile(v):
			print "\tFound valid visual studio " + keyname + " at " + val
			studios.append( (keyname, v) )
	except WindowsError:
		# break
		pass # we get a windows error before we are done, keep going!
if len(studios) == 0:
	raise Error("No installed visual studios found!")
print "Found " + str(len(studios)) + " visual studios = GOOD"

print "-----------------------------------------------------------------"
print

if os.path.isdir(wd):
	print "libxml2 structure seems valid!"
else:
	print "ERROR: libxml2 structure NOT VALID!"
	print "The layout should be:"
	print "  build.py"
	print "+ libxml2"
	print "|- doc"
	print "|- include"
	print "|- win32"
	print
	print "That is win32 folder should exist at", wd

print "This script will build libxml2 and place result in " + os.path.join(cwd, "build")
if raw_input("Continue? (y/n): ").lower() == "y":
	print "-----------------------------------------------------------------"
	print

	for studio in studios:
		for p in plattforms:
			for c in conf:
				sn = os.path.basename(studio).strip()
				snf = sn.replace(".", "").replace(" ", "_")
				print "Building " + sn + " " + c + " " + p
				debug="debug=no"
				if c=="debug":
					debug="debug=yes"
				folder = "..\\..\\build\\" + snf + "\\" + p + "-"+ c
				absfolder = os.path.normpath(os.path.join(wd, folder))
				if os.path.isdir(absfolder) == False:
					os.makedirs(absfolder)
				cmdstudio = "@call \"" + studio +"\\"+ VCVARS + "\" " + p + " 1>\"" + folder + "\\vcvars.log\" 2>&1"
				cmdgen = "@cscript configure.js "+debug+" ftp=no http=no compiler=msvc static=yes prefix=\""+folder+"\" vcmanifest=yes iconv=no zlib=no 1>\"" + folder + "\\gen.log\" 2>&1"
				cmdmake = "@nmake.exe /f makefile.msvc rebuild install 1>\"" + folder + "\\build.log\" 2>&1"
				cmdfile = os.path.join(wd, "build-" + snf + "-" + c +"-" + p +".bat");
				with open(cmdfile, "w") as f:
					print >>f, cmdstudio
					print >>f, cmdgen
					print >>f, cmdmake
					print >>f, "@del Makefile"
				Run(wd, cmdfile)
				
	print "-----------------------------------------------------------------"
	print
	print "All done, please check the log files for errors"

Run(cwd, "pause")