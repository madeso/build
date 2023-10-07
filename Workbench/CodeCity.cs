using Spectre.Console;
using StbRectPackSharp;
using System.Collections.Immutable;
using Workbench.Doxygen.Compound;

namespace Workbench.CodeCity;

// based on/inspired by https://wettel.github.io/codecity.html

internal record Vec3(float X, float Y, float Z)
{
    public static readonly Vec3 Zero = new(0, 0, 0);

    public static Vec3 operator+(Vec3 a)
        => a;
    public static Vec3 operator -(Vec3 a)
        => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator +(Vec3 a, Vec3 b)
        => new(a.X + b.X, a.Y+b.Y, a.Z+b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b)
        => a + (-b);

    public override string ToString()
        => $"({X}, {Y}, {Z})";

    public static Vec3 Max(Vec3 a, Vec3 b)
        => new(Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), Math.Max(a.Z, b.Z));
}

internal class Cube
{
    public Cube(string name, List<Cube> children, Vec3 position, Vec3 size, int colorSource)
    {
        Name = name;
        Children = children;
        Position = position;
        Size = size;
        ColorSource = colorSource;
    }

    public string Name {get;}
    public List<Cube> Children {get;}
    public Vec3 Position {get;set;}
    public Vec3 Size {get;}
    public int ColorSource {get; }

    public static Vec3 GetMax(IEnumerable<Cube> subs)
        => subs
        .SelectMany(c => new Vec3[] { c.Position, c.Position + c.Size })
        .Aggregate(Vec3.Zero, Vec3.Max)
        ;
}

internal class Facade
{
    internal static List<Cube> Collect(Printer printer, string doxygenXml)
    {
        const float MIN_SIZE = 2;
        const float NS_SPACING = 1;
        const float NS_HEIGHT = MIN_SIZE + 2;
        const float CLASS_SIZE_MOD = 1;
        const float CLASS_HEIGHT_MOD = 1;

        printer.Info("Parsing doxygen XML...");
        var dox = Doxygen.Doxygen.ParseIndex(doxygenXml);

        printer.Info("Collecting functions...");
        var namespaces = DoxygenUtils.AllNamespaces(dox).ToImmutableArray();

        var allNamespacesInNamespaces = namespaces
            .SelectMany(ns => ns.InnerNamespaces)
            .Select(ns => ns.refid)
            .ToImmutableHashSet();
        var allClassesInNamespaces = namespaces
            .SelectMany(ns => ns.InnerClasses)
            .Select(k => k.refid)
            .ToImmutableHashSet();

        var rootNamespaces = namespaces
            .Where(ns => allNamespacesInNamespaces.Contains(ns.Id) == false);
        var rootClasses = DoxygenUtils.AllClasses(dox)
            .Where(x => allClassesInNamespaces.Contains(x.Id) == false);
        // todo(Gustav): the root class seems to exclude a few to many, investigate!

        return CollectFromNamespace(dox, rootNamespaces, rootClasses);

        static List<Cube> CollectFromNamespace(Doxygen.Index.DoxygenType dox
            , IEnumerable<CompoundDef> namespaces
            , IEnumerable<CompoundDef> classes)
        {
            var cubes = new List<Cube>();

            // add inner namespaces
            foreach(var ns in namespaces)
            {
                int depth = GetNamespaceDepth(dox, ns);

                var sub = CollectFromNamespace(dox
                    , DoxygenUtils.IterateNamespacesInNamespace(dox, ns)
                    , DoxygenUtils.IterateClassesInNamespace(dox, ns)
                    );
                var max = Cube.GetMax(sub);
                
                foreach(var c in sub)
                {
                    Move(c, new(NS_SPACING, NS_HEIGHT, NS_SPACING));
                }

                cubes.Add(new(ns.CompoundName, sub, Vec3.Zero,
                    new(max.X + NS_SPACING * 2, NS_HEIGHT, max.Z + NS_SPACING * 2), depth));
            }

            // add inner classes
            foreach (var klass in classes)
            {
                // todo(Gustav): handle proeprties better
                var numberOfVariables = DoxygenUtils.AllMembersForAClass(klass)
                    .Where(mem => mem.Kind == DoxMemberKind.Variable)
                    .Count();
                var numberOfMethods = DoxygenUtils.AllMembersForAClass(klass)
                    .Where(mem => mem.Kind == DoxMemberKind.Function)
                    .Count();
                var funcLoc = DoxygenUtils.AllMembersForAClass(klass)
                    .Where(mem => mem.Kind == DoxMemberKind.Function)
                    .Select(mem => LengthOfCode(mem.Location))
                    .Sum();
                var loc = funcLoc;

                var size = (numberOfVariables + MIN_SIZE) * CLASS_SIZE_MOD;
                var height = (numberOfMethods + MIN_SIZE) * CLASS_HEIGHT_MOD;

                cubes.Add(new(klass.CompoundName, new(), Vec3.Zero, new(size, height, size), loc));
            }

            // layout all cubes
            {
                Packer packer = new(2, 4);

                foreach(var c in cubes)
                {
                    PackRect(ref packer, (int) Math.Ceiling(c.Size.X), (int) Math.Ceiling(c.Size.Z), c);
                }

                foreach (var r in packer.PackRectangles)
                {
                    var c = (Cube) r.Data;
                    MoveTo(c, new(r.X, 0, r.Y));
                }
            }

            return cubes;
        }

        static void Move(Cube cube, Vec3 diff)
        {
            cube.Position += diff;
            foreach(var child in cube.Children)
            {
                Move(child, diff);
            }
        }

        static void MoveTo(Cube cube, Vec3 to)
        {
            Move(cube, to - cube.Position);
        }

        static void PackRect(ref Packer packer, int width, int height, Cube c)
        {
            var pr = packer.PackRect(width, height, c);

            // If pr is null, it means there's no place for the new rect
            // Double the size of the packer until the new rectangle will fit
            while (pr == null)
            {
                // double the size
                var newWidth = packer.Width;
                var newHeight = packer.Height;
                if(newWidth > newHeight)
                {
                    newHeight *= 2;
                }
                else
                {
                    newWidth *= 2;
                }
                var newPacker = new Packer(newWidth, newHeight);
                foreach (var existingRect in packer.PackRectangles)
                {
                    newPacker.PackRect(existingRect.Width, existingRect.Height, existingRect.Data);
                }
                packer.Dispose();
                packer = newPacker;

                // try again
                pr = packer.PackRect(width, height, c);
            }
        }

        static int LengthOfCode(locationType? location)
        {
            if(location == null) return 0;

            var start = location.bodystart;
            var end = location.bodyend;
            
            if(start.HasValue == false) return 0;
            if (end.HasValue == false) return 0;

            return end.Value - start.Value;
        }

        static int GetNamespaceDepth(Doxygen.Index.DoxygenType dox, CompoundDef rootNamespace)
        {
            return DoxygenUtils.IterateNamespacesInNamespace(dox, rootNamespace)
                .Select(nspace => GetNamespaceDepth(dox, nspace) )
                .DefaultIfEmpty(0)
                .Max() + 1;
        }
    }

    internal static IEnumerable<string> HtmlLines(string title, List<Cube> cubes)
    {
        var version = "0.157.0";

        yield return "<!DOCTYPE html>";
        yield return "<html lang=\"en\">";
        yield return "    <head>";
        yield return $"        <title>{title}</title>";
        yield return "        <meta charset=\"utf-8\">";
        yield return "        <meta name=\"viewport\" content=\"width=device-width, user-scalable=no, minimum-scale=1.0, maximum-scale=1.0\">";
        yield return "        <style>";
        yield return "            body {";
        yield return "                background-color: #bfe3dd;";
        yield return "                color: #000;";
        yield return "                margin: 0;";
        yield return "            }";
        yield return "            a {";
        yield return "                color: #2983ff;";
        yield return "            }";
        yield return "            #info {";
        yield return "                position: absolute;";
        yield return "                top: 0px;";
        yield return "                width: 100%;";
        yield return "                padding: 10px;";
        yield return "                box-sizing: border-box;";
        yield return "                text-align: center;";
        yield return "                -moz-user-select: none;";
        yield return "                -webkit-user-select: none;";
        yield return "                -ms-user-select: none;";
        yield return "                user-select: none;";
        yield return "                pointer-events: none;";
        yield return "                z-index: 1;";
        yield return "            }";
        yield return "        </style>";
        yield return "    </head>";
        yield return "    <body>";
        yield return "        <div id=\"container\"></div>";
        yield return "        <div id=\"info\">";
        yield return "            Hello world";
        yield return "        </div>";
        yield return "        <script type=\"importmap\">";
        yield return "            {";
        yield return "                \"imports\": {";
        yield return $"                  \"three\": \"https://unpkg.com/three@{version}/build/three.module.js\",";
        yield return $"                  \"three/addons/\": \"https://unpkg.com/three@{version}/examples/jsm/\"";
        yield return "                }";
        yield return "            }";
        yield return "        </script>";
        yield return "        <script type=\"module\">";
        yield return "            import * as THREE from 'three';";
        yield return "            import Stats from 'three/addons/libs/stats.module.js';";
        yield return "            import { OrbitControls } from 'three/addons/controls/OrbitControls.js';";
        yield return "            const container = document.getElementById( 'container' );";
        yield return "            const renderer = new THREE.WebGLRenderer( { antialias: true } );";
        yield return "            renderer.setPixelRatio( window.devicePixelRatio );";
        yield return "            renderer.setSize( window.innerWidth, window.innerHeight );";
        yield return "            container.appendChild( renderer.domElement );";
        yield return "            const scene = new THREE.Scene();";
        yield return "            const MakeCube = (name, color, sx, sy, sz, x, y, z) => {";
        yield return "                const geometry = new THREE.BoxGeometry(sx, sy, sz);";
        yield return "                const material = new THREE.MeshLambertMaterial( { color: color } );";
        yield return "                const cube = new THREE.Mesh( geometry, material );";
        yield return "                cube.position.x = x;";
        yield return "                cube.position.y = y;";
        yield return "                cube.position.z = z;";
        yield return "                cube.name = name;";
        yield return "                scene.add(cube);";
        yield return "            };";
        yield return "            scene.background = new THREE.Color( 0xbfe3dd );";
        yield return "            const camera = new THREE.PerspectiveCamera( 45, window.innerWidth / window.innerHeight, 1, 10000);";

        var extent = Cube.GetMax(cubes);
        yield return $"            camera.position.set({extent.X}, {extent.Y / 2}, {extent.Z} );";
        yield return "            const controls = new OrbitControls( camera, renderer.domElement )";
        yield return $"            controls.target.set({extent.X/2}, 0, {extent.Z/2} );";


        yield return "            const dirLight1 = new THREE.DirectionalLight( 0xffffff, 3 );";
        yield return "            dirLight1.position.set( 1, 1, 1 );";
        yield return "            scene.add( dirLight1 );";
        yield return "            const dirLight2 = new THREE.DirectionalLight( 0x002288, 3 );";
        yield return "            dirLight2.position.set( - 1, - 1, - 1 );";
        yield return "            scene.add( dirLight2 );";
        yield return "            const ambientLight = new THREE.AmbientLight( 0x555555 );";
        yield return "            scene.add( ambientLight );";


        Random r = new Random();
        char RHex()
        {
            const string HEX = "0123456789abcdef";
            return HEX[r.Next(HEX.Length)];
        }
        foreach(var c in DepthFirst(cubes))
        {
            // todo(Gustav): use calcualted color instead of random
            var color = $"0x{RHex()}{RHex()}{RHex()}{RHex()}{RHex()}{RHex()}";

            // todo(Gustav): escape name
            yield return $"            MakeCube(\"{c.Name}\", {color}, {c.Size.X}, {c.Size.Y}, {c.Size.Z}," +
                $" {c.Position.X + c.Size.X/2}, {c.Position.Y + +c.Size.Y / 2}, {c.Position.Z + +c.Size.Z / 2});";
        }

        yield return "            let inter = null;";
        yield return "            const pointer = new THREE.Vector2();";
        yield return "            const raycaster = new THREE.Raycaster();";
        yield return "            function onPointerMove(event) {";
        yield return "                pointer.x = ( event.clientX / window.innerWidth ) * 2 - 1;";
        yield return "                pointer.y = - ( event.clientY / window.innerHeight ) * 2 + 1;";
        yield return "            }";
        yield return "            document.addEventListener('mousemove', onPointerMove);";
        yield return "            const intersect = () => {";
        yield return "                raycaster.setFromCamera( pointer, camera );";
        yield return "                const intersects = raycaster.intersectObjects( scene.children, false );";
        yield return "                if (intersects.length > 0)";
        yield return "                {";
        yield return "                    const newInter = intersects[0].object;";
        yield return "                    if (inter != newInter)";
        yield return "                    {";
        // todo(Gustav): also highlight parent/child?
        yield return "                        if (inter) inter.material.emissive.setHex( inter.currentHex );";
        yield return "                        inter = newInter;";
        yield return "                        inter.currentHex = inter.material.emissive.getHex();";
        yield return "                        inter.material.emissive.setHex( 0xff0000 );";
        // todo(Gustav): escape name or use html
        yield return "                        info.innerHTML = inter.name";
        yield return "                    }";
        yield return "                }";
        yield return "                else";
        yield return "                {";
        yield return "                    if (inter)";
        yield return "                    {";
        yield return "                        inter.material.emissive.setHex( inter.currentHex );";
        yield return "                        info.innerHTML = \"\";";
        yield return "                    }";
        yield return "                    inter = null;";
        yield return "                }";
        yield return "            };";
        yield return "            const clock = new THREE.Clock();";
        yield return "            window.onresize = function () {";
        yield return "                camera.aspect = window.innerWidth / window.innerHeight;";
        yield return "                camera.updateProjectionMatrix();";
        yield return "                renderer.setSize( window.innerWidth, window.innerHeight );";
        yield return "            };";
        yield return "            function animate() {";
        yield return "                requestAnimationFrame( animate );";
        yield return "                controls.update();";
        yield return "                intersect();";
        yield return "                renderer.render( scene, camera );";
        yield return "            }";
        yield return "            animate();";
        yield return "        </script>";
        yield return "    </body>";
        yield return "</html>";

        static IEnumerable<Cube> DepthFirst(IEnumerable<Cube> cubes)
        {
            foreach(var c in cubes)
            {
                yield return c;
                foreach(var child in DepthFirst(c.Children))
                {
                    yield return child;
                }
            }
        }
    }
}
