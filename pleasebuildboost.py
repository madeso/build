#!/usr/bin/env python

from subprocess import call
import os
import shutil
import argparse
from _winreg import *
import errno
import glob

################################################################################################################################
###################################################################################################################### UTIL CODE
def shutilrmtree(x):
	if os.path.exists(x):
		shutil.rmtree(x)

def make_sure_path_exists(path):
	try:
		os.makedirs(path)
	except OSError as exception:
		if exception.errno != errno.EEXIST:
			raise

class Error(Exception):
	def __init__(self, value):
		self.value = value
	def __str__(self):
		return repr(self.value)

def Run(mywd, mycmd):
	try:
		retcode = call(mycmd, cwd=mywd, shell=True)
		if retcode < 0:
			print >>sys.stderr, "Child was terminated by signal", -retcode
		#else ifmyspeak:
		#	print >>sys.stderr, "Child returned", retcode
	except OSError, e:
		print "Execution failed:", e

def CopyFiles(src, dest):
	if os.path.exists(src):
		for dirpath, dirnames, filenames in os.walk(src):
			for f in filenames:
				fn = os.path.join(dirpath, f)
				if (os.path.isfile(fn)):
					shutil.copy(fn, dest)

################################################################################################################################
############################################################################################################## LOGIC STARTS HERE

############################################# argument parsing code

parser = argparse.ArgumentParser(description='Build the boost library')
parser.add_argument("build", help="The folder where to build stuff")
parser.add_argument("-libraries", metavar="LIB", nargs="*", help="the boost libraries to build")
parser.add_argument("-includes", metavar="LIB", nargs="*", help="the boost includes to copy")
parser.add_argument("-singlebuild", metavar="B", help="specify compilername:bit to only build a single instance, 2010:x64")
parser.add_argument("-nobcp", dest='bcp', action='store_const', const=False, default=True, help="By default this will run bcp, if specified this will make the buildscript to not run bcp")
args = parser.parse_args()

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
			studioversion = keyname
			if keyname == '9.0': keyname="2008"
			elif keyname == '10.0': keyname="2010"
			elif keyname == '11.0': keyname="2012"
			elif keyname == '12.0': keyname="2013"
			elif keyname == '14.0': keyname="2015"
			print "\tFound valid visual studio " + keyname + " at " + val
			studios.append( (studioversion, keyname, v) )
	except WindowsError:
		# break
		pass # we get a windows error before we are done, keep going!
if len(studios) == 0:
	raise Error("No installed visual studios found!")
print "Found " + str(len(studios)) + " visual studios = GOOD"

############################################# validate current working directory...
cwd = os.getcwd()
print "Current dir: ", cwd
if os.path.exists(os.path.join(cwd, "boost.png")) == False:
	raise Error("Failed to find boost.png, doesnt look like a valid root boost directory")
if os.path.exists(os.path.join(cwd, "Jamroot")) == False:
	raise Error("Failed to find Jamroot, doesnt look like a valid root boost directory")
print "Looks like a boost directory = GOOD"

############################################# run the bootstrapper if it's not run
if os.path.exists(os.path.join(cwd, "bjam.exe")) == False:
	print "Bootstrap has not been run, running bootstrapper..."
	Run(cwd, "bootstrap")
	if os.path.exists(os.path.join(cwd, "bjam.exe")) == False:
		raise Error("Bootrapping failed!")
print "bootstrapping looks ok = GOOD"

############################################# generate bcp if it's not existing
# for convenience we place a copy of bcp in the root and check for this instead
if args.bcp:
	if os.path.exists(os.path.join(cwd, "bcp.exe")) == False:
		if os.path.exists(os.path.join(cwd, "dist/bin/bcp.exe")) == False:
			print "BCP is not built, bulding..."
			Run(cwd, "b2 tools/bcp")
			if os.path.exists(os.path.join(cwd, "dist/bin/bcp.exe")) == False:
				raise Error("Failed to build bcp!")
		print "Distributing bcp..."
		shutil.copy(os.path.join(cwd, "dist/bin/bcp.exe"), cwd)
		if os.path.exists(os.path.join(cwd, "bcp.exe")) == False:
			raise Error("Failed to distribute bcp!")
	print "bcp looks ok = GOOD"
else:
	print "bcp check ignored = should be OK"

############################################# find out what libraries to build

libraries = args.libraries or []
if len(libraries) == 0:
	print "No libraries specified, adding custom libraries..."
	libraries = ["date_time", "filesystem", "regex", "serialization", "signals", "system", "thread"]

includes = args.includes or []
if len(includes) == 0:
	print "No includes specified, adding custom includes..."
	includes = ["shared_ptr", "foreach", "property_tree", "algorithm"]

print "libraries to build ", libraries
print "includes to copy ", includes
	
############################################# ready to build boost now
print "Building boost..."
buildfolder = args.build
tempfolder = os.path.join(buildfolder, "temp")
includefolder = os.path.join(buildfolder, "include")
distfolder = os.path.join(buildfolder, "dist")
shutilrmtree(tempfolder)
shutilrmtree(includefolder)
shutilrmtree(distfolder)
cmdfile = os.path.join(cwd, "pleasebuildboost-sub.bat")

# include
if args.bcp:
	print "Copying include folder (running bcp)..."
	make_sure_path_exists(includefolder)
	bcpline = "bcp " + ' '.join(libraries) + " " + ' '.join(includes) + " " + includefolder + " > " + os.path.join(buildfolder, "bcp.txt") + " 2>&1"
	Run(cwd, bcpline)
	# remove useless data
	shutilrmtree(os.path.join(includefolder, "doc"))
	shutilrmtree(os.path.join(includefolder, "libs"))
	os.remove(os.path.join(includefolder, "boost.css"))
	os.remove(os.path.join(includefolder, "boost.png"))
	os.remove(os.path.join(includefolder, "Jamroot"))
else:
	print "Not running bcp"

# parse buildstatus
singlebuild = False
singlestudio,singlebit = '', ''
if args.singlebuild != None:
	singlebuild = True
	singlestudio,singlebit = tuple(args.singlebuild.split(":",1))
	print "Singlebuild: ", singlestudio, singlebit

# dist
print "Building dist..."
for c in ["debug", "release"]:
	print "================================================="
	print "Configuration: ", c
	print "================================================="
	for bit, bitname in [('32', 'win32'), ('64', 'x64')]:
		print "Building ", bitname
		print "--------------"
		for studio in studios:
			studioversion, studioname, vcvarspath = studio
			targetfolder = os.path.join(distfolder, "vs" + studioname + bitname, c)
			if singlebuild == False or ( singlestudio==studioname and singlebit==bitname ):
				make_sure_path_exists(targetfolder)
				make_sure_path_exists(tempfolder)
				print "Building to ", targetfolder
				cmdstudio = "@call " + vcvarspath + " > " + os.path.join(targetfolder, "vcvars.txt") + " 2>&1"
				cmdbuild = "@b2 --toolset=msvc-"+studioversion+" --prefix="+os.path.join(tempfolder, "build")+" --stagedir="+os.path.join(tempfolder, "stage")+" --build-dir="+os.path.join(tempfolder, "temp")+" "
				cmdbuild = cmdbuild + " address-model="+bit + " link=static variant=" + c + " "
				for l in libraries:
					cmdbuild = cmdbuild + " --with-" + l
				cmdbuild = cmdbuild + " > " + os.path.join(targetfolder, "build.txt") + " 2>&1"
				with open(cmdfile, "w") as f:
					print >>f, cmdstudio
					print >>f, cmdbuild
				Run(cwd, cmdfile)
				CopyFiles(os.path.join(tempfolder, "stage"), targetfolder)
				shutil.copy(cmdfile, targetfolder)
				os.remove(cmdfile)
				shutil.rmtree(tempfolder)
			else:
				print "Ignoring ", studioname, bitname