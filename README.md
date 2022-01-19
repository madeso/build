# workbench (wb)
WIP Port of my crappy (python) build scripts to rust

## name
The names comes from that I couldn't name the binary build so I went to wikipedia 'tool' and looked for a suitable name, workbench seemed to fit. https://en.wikipedia.org/wiki/Workbench_(woodworking)

## old readme
Instead of keeping my crappy build scripts to build various things in a dropbox folder somewhere, I might aswell dump in a github project so I get version control and others can be subjected to the horrors (or might find it useful)

## helpful regex for converting python to rust

Replace : with {}
    ^([ ]*)([^:]*):$
    $1$2\n$1{\n$1}

Optional -> Option
    typing\.Optional\[([^\]]+)\]
    Option<$1>
