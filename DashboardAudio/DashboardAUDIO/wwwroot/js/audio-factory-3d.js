/**
 * Audio Factory 3D Floor Plan (1:1 CAD Reference Visualization)
 * Panasonic Manufacturing Indonesia (PMI) - Audio Business Unit
 * Powered by Three.js
 */

(function (window) {
    'use strict';

    let scene, camera, renderer, controls;
    let container;
    let animationFrameId;
    let isAutoRotating = false;
    let linesMap = new Map(); // id -> { group, conveyorMesh, andonMesh, andonLight, glowStrip, data, products: [] }
    let animatedProducts = [];
    let pulseTime = 0;
    let areaLabels = [];
    let lineMarkers2D = [];
    let raycaster, mouse;
    let hoveredLineId = null;
    let isLightMode = false;

    // Factory Dimensions (Normalized coordinate system: ~200 x 100)
    const W = 200; // Left (South, -100) to Right (North, +100)
    const D = 96;  // Top (West, -48) to Bottom (East, +48)
    const WALL_H = 3.6;

    // Color Palette
    const PALETTE = {
        floorDark: 0x1A1E33,
        floorGridDark: 0x333A5C,
        floorLight: 0xE8ECF2,
        floorGridLight: 0xC5CED9,
        aisleDark: 0x222742,
        aisleLine: 0xF59E0B, // Safety yellow
        wallDark: 0x3D4668,
        wallGlass: 0x60A5FA,
        conveyorBelt: 0x1F2438,
        conveyorFrame: 0x475569,
        rackMagenta: 0xEC4899,
        rackBinYellow: 0xFBBF24,
        rackBinBlue: 0x38BDF8,
        pcbMachine: 0x0D9488,
        pcbAccent: 0x14B8A6,
        heatMachine: 0xC2410C,
        packingGreen: 0x16A34A,
        soundAcoustic: 0x334155,
        officeDesk: 0x64748B,
        officeGlass: 0x93C5FD,
        statusGreen: 0x10B981,
        statusYellow: 0xF59E0B,
        statusRed: 0xEF4444
    };

    function initAudioFactory3D(containerElementId) {
        container = document.getElementById(containerElementId);
        if (!container) {
            console.error('[AudioFactory3D] Container element not found:', containerElementId);
            return;
        }

        container.style.position = 'relative';
        container.style.overflow = 'hidden';

        const width = container.clientWidth || 600;
        const height = container.clientHeight || 450;

        // 1. Scene
        scene = new THREE.Scene();
        scene.background = new THREE.Color(isLightMode ? 0xE2E8F0 : 0x181B2D);

        // 2. Camera (Isometric Perspective)
        camera = new THREE.PerspectiveCamera(36, width / height, 1, 1000);
        camera.position.set(0, 165, 140);
        camera.lookAt(0, 0, 5);

        // 3. Renderer
        renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: 'high-performance' });
        renderer.setSize(width, height);
        renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        renderer.shadowMap.enabled = true;
        renderer.shadowMap.type = THREE.PCFSoftShadowMap;
        container.appendChild(renderer.domElement);

        // 4. OrbitControls
        if (typeof THREE.OrbitControls !== 'undefined') {
            controls = new THREE.OrbitControls(camera, renderer.domElement);
            controls.enableDamping = true;
            controls.dampingFactor = 0.08;
            controls.maxPolarAngle = Math.PI / 2.15; // Don't go below floor
            controls.minDistance = 40;
            controls.maxDistance = 320;
            controls.target.set(0, 0, 5);
        }

        // 5. Lighting
        setupLighting();

        // 6. Build Factory Floor & Structures (1:1 with CAD Floor Plan)
        buildFloor();
        buildPerimeterWalls();
        buildRestAndFacilitiesArea(); // Bottom-Left (Musholla, Toilets, Rest Area)
        buildMaterialWarehouseArea();  // Left & Bottom Racks (Magenta Racks)
        buildHeatInductionArea();      // Left-Center
        buildPackingArea();            // Top-Center
        buildFinalAssemblyLines();     // Center: 11 Lines (6 left, 5 right)
        buildPCBAssemblyArea();        // Top-Right: SMT & PCB Lines
        buildSpecializedWorkshops();   // Bottom: Solder Bar & Workshop
        buildSoundAndTestingRooms();   // Right: Acoustic Soundrooms & Shield Room
        buildOfficesArea();            // Far Right: Main Office, VIP, Guest

        // 7. Raycaster & Interactivity
        raycaster = new THREE.Raycaster();
        mouse = new THREE.Vector2();

        renderer.domElement.addEventListener('mousemove', onMouseMove, false);
        renderer.domElement.addEventListener('click', onMouseClick, false);
        window.addEventListener('resize', onWindowResize, false);

        // Create overlay container for 3D floating markers & tooltips
        createUIOverlays();

        // Start Animation Loop
        animate();
    }

    // ─────────────────────────────────────────────────────────────
    // LIGHTING
    // ─────────────────────────────────────────────────────────────
    let ambientLight, dirLight, cyanLight, amberLight;
    function setupLighting() {
        ambientLight = new THREE.AmbientLight(0xffffff, isLightMode ? 0.85 : 0.65);
        scene.add(ambientLight);

        dirLight = new THREE.DirectionalLight(0xffffff, isLightMode ? 0.9 : 0.7);
        dirLight.position.set(60, 120, 80);
        dirLight.castShadow = true;
        dirLight.shadow.mapSize.width = 1024;
        dirLight.shadow.mapSize.height = 1024;
        dirLight.shadow.camera.near = 10;
        dirLight.shadow.camera.far = 300;
        dirLight.shadow.camera.left = -120;
        dirLight.shadow.camera.right = 120;
        dirLight.shadow.camera.top = 70;
        dirLight.shadow.camera.bottom = -70;
        scene.add(dirLight);

        // Subtle tech cyan accent light
        cyanLight = new THREE.PointLight(0x00f0ff, 0.4, 250);
        cyanLight.position.set(-60, 45, -30);
        scene.add(cyanLight);

        // Warm amber accent light
        amberLight = new THREE.PointLight(0xf59e0b, 0.35, 250);
        amberLight.position.set(60, 40, 30);
        scene.add(amberLight);
    }

    // ─────────────────────────────────────────────────────────────
    // 1. FLOOR & WALKWAYS
    // ─────────────────────────────────────────────────────────────
    let floorMesh;
    function buildFloor() {
        // Main base slab
        const floorGeo = new THREE.BoxGeometry(W + 2, 1, D + 2);
        const floorMat = new THREE.MeshStandardMaterial({
            color: isLightMode ? PALETTE.floorLight : PALETTE.floorDark,
            roughness: 0.65,
            metalness: 0.15
        });
        floorMesh = new THREE.Mesh(floorGeo, floorMat);
        floorMesh.position.y = -0.5;
        floorMesh.receiveShadow = true;
        scene.add(floorMesh);

        // Subtle grid overlay
        const grid = new THREE.GridHelper(W, 40, isLightMode ? 0x94A3B8 : 0x2E375A, isLightMode ? 0xCBD5E1 : 0x232942);
        grid.position.y = 0.05;
        scene.add(grid);

        // Walkway yellow safety lines (marking main logistics arteries)
        createWalkwayMarking(-10, 0, 4, D - 6);   // Main central north-south corridor
        createWalkwayMarking(0, -22, W - 10, 3);  // Cross corridor between Packing/PCB and Assembly
        createWalkwayMarking(0, 26, W - 10, 3);   // Cross corridor between Assembly and Bottom Rooms
        createWalkwayMarking(-52, 0, 3, D - 6);   // Material to Assembly corridor
        createWalkwayMarking(55, 0, 3, D - 6);    // Assembly to Office corridor
    }

    function createWalkwayMarking(x, z, w, d) {
        const geo = new THREE.PlaneGeometry(w, d);
        const mat = new THREE.MeshBasicMaterial({
            color: PALETTE.aisleDark,
            side: THREE.DoubleSide,
            depthWrite: false
        });
        const plane = new THREE.Mesh(geo, mat);
        plane.rotation.x = -Math.PI / 2;
        plane.position.set(x, 0.08, z);
        scene.add(plane);

        // Dashed border / stripe
        const edges = new THREE.EdgesGeometry(geo);
        const lineMat = new THREE.LineBasicMaterial({ color: 0xF59E0B, transparent: true, opacity: 0.45 });
        const line = new THREE.LineSegments(edges, lineMat);
        line.rotation.x = -Math.PI / 2;
        line.position.set(x, 0.09, z);
        scene.add(line);
    }

    // ─────────────────────────────────────────────────────────────
    // 2. PERIMETER WALLS (Architectural Cutaway 3D)
    // ─────────────────────────────────────────────────────────────
    function buildPerimeterWalls() {
        const wallMat = new THREE.MeshStandardMaterial({
            color: PALETTE.wallDark,
            roughness: 0.5,
            metalness: 0.3
        });

        // Top Wall (West)
        createWall(-55, -D / 2, 90, 1.2, WALL_H, wallMat); // Left section
        createWall(55, -D / 2, 90, 1.2, WALL_H, wallMat);  // Right section (leave door gap)

        // Bottom Wall (East)
        createWall(0, D / 2, W, 1.2, WALL_H, wallMat);

        // Left Wall (South)
        createWall(-W / 2, 0, 1.2, D, WALL_H, wallMat);

        // Right Wall (North)
        createWall(W / 2, 0, 1.2, D, WALL_H, wallMat);

        // Building columns along grid
        for (let x = -80; x <= 80; x += 40) {
            for (let z = -36; z <= 36; z += 36) {
                createColumn(x, z);
            }
        }
    }

    function createWall(x, z, w, d, h, mat) {
        const geo = new THREE.BoxGeometry(w, h, d);
        const mesh = new THREE.Mesh(geo, mat);
        mesh.position.set(x, h / 2, z);
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        scene.add(mesh);
        return mesh;
    }

    function createGlassWall(x, z, w, d, h) {
        const geo = new THREE.BoxGeometry(w, h, d);
        const mat = new THREE.MeshStandardMaterial({
            color: PALETTE.wallGlass,
            roughness: 0.1,
            metalness: 0.8,
            transparent: true,
            opacity: 0.35
        });
        const mesh = new THREE.Mesh(geo, mat);
        mesh.position.set(x, h / 2, z);
        scene.add(mesh);

        // Top metal cap
        const capGeo = new THREE.BoxGeometry(w, 0.2, d);
        const capMat = new THREE.MeshStandardMaterial({ color: 0x94A3B8, metalness: 0.8 });
        const cap = new THREE.Mesh(capGeo, capMat);
        cap.position.set(x, h + 0.1, z);
        scene.add(cap);
        return mesh;
    }

    function createColumn(x, z) {
        const geo = new THREE.BoxGeometry(2, WALL_H * 1.1, 2);
        const mat = new THREE.MeshStandardMaterial({ color: 0x475569, metalness: 0.5 });
        const col = new THREE.Mesh(geo, mat);
        col.position.set(x, (WALL_H * 1.1) / 2, z);
        col.castShadow = true;
        scene.add(col);

        // Yellow safety warning stripes at bottom
        const stripeGeo = new THREE.BoxGeometry(2.1, 0.8, 2.1);
        const stripeMat = new THREE.MeshBasicMaterial({ color: 0xF59E0B });
        const stripe = new THREE.Mesh(stripeGeo, stripeMat);
        stripe.position.set(x, 0.4, z);
        scene.add(stripe);
    }

    // ─────────────────────────────────────────────────────────────
    // 3. REST & FACILITIES AREA (Bottom Left)
    // ─────────────────────────────────────────────────────────────
    function buildRestAndFacilitiesArea() {
        const areaX = -78, areaZ = 38;
        // Partition walls
        createGlassWall(-64, areaZ, 0.8, 18, WALL_H * 0.85); // Room east wall
        createGlassWall(areaX, 29, 28, 0.8, WALL_H * 0.85);  // Room north wall

        // Musholla Green Carpet
        const carpetGeo = new THREE.PlaneGeometry(12, 10);
        const carpetMat = new THREE.MeshStandardMaterial({ color: 0x059669, roughness: 0.9 });
        const carpet = new THREE.Mesh(carpetGeo, carpetMat);
        carpet.rotation.x = -Math.PI / 2;
        carpet.position.set(-71, 0.1, 41);
        scene.add(carpet);

        // Rest Area Sofa / Benches
        createBench(-86, 35, 8, 2);
        createBench(-86, 42, 8, 2);

        addAreaLabel('MUSHOLLA & REST', -75, 41);
    }

    function createBench(x, z, w, d) {
        const geo = new THREE.BoxGeometry(w, 0.8, d);
        const mat = new THREE.MeshStandardMaterial({ color: 0x3B82F6, roughness: 0.5 });
        const bench = new THREE.Mesh(geo, mat);
        bench.position.set(x, 0.4, z);
        scene.add(bench);
    }

    // ─────────────────────────────────────────────────────────────
    // 4. MATERIAL WAREHOUSE AREA (Left & Bottom Wall Racks)
    // ─────────────────────────────────────────────────────────────
    function buildMaterialWarehouseArea() {
        // High density pallet rack grid in upper-left (magenta racks)
        const startX = -92, startZ = -40;
        const cols = 6, rows = 4;
        const spacingX = 6.2, spacingZ = 6.0;

        for (let r = 0; r < rows; r++) {
            for (let c = 0; c < cols; c++) {
                const rx = startX + c * spacingX;
                const rz = startZ + r * spacingZ;
                createPalletRack(rx, rz, 5.2, 2.2, 5.0, PALETTE.rackMagenta);
            }
        }

        // Long row of 17 material carts along bottom wall (Z: 38 to 44, X: -48 to 5)
        for (let i = 0; i < 15; i++) {
            const rx = -48 + i * 3.5;
            createPalletRack(rx, 42, 2.8, 1.8, 3.8, PALETTE.rackMagenta);
        }

        // Hoist & Table Lift on top wall
        createLiftUnit(-65, -45);
        createHoistUnit(-80, -45);

        addAreaLabel('MATERIAL WAREHOUSE', -75, -20);
    }

    function createPalletRack(x, z, w, d, h, rackColor) {
        const group = new THREE.Group();
        const frameMat = new THREE.MeshStandardMaterial({ color: rackColor, metalness: 0.4, roughness: 0.5 });
        const postGeo = new THREE.BoxGeometry(0.2, h, 0.2);

        // 4 corner posts
        const hw = w / 2, hd = d / 2;
        [[-hw, -hd], [hw, -hd], [-hw, hd], [hw, hd]].forEach(([px, pz]) => {
            const post = new THREE.Mesh(postGeo, frameMat);
            post.position.set(px, h / 2, pz);
            group.add(post);
        });

        // 3 shelf tiers with bins
        const shelfGeo = new THREE.BoxGeometry(w, 0.15, d);
        const shelfMat = new THREE.MeshStandardMaterial({ color: 0x94A3B8, metalness: 0.6 });
        const binColors = [PALETTE.rackBinYellow, PALETTE.rackBinBlue, 0x10B981, 0xEC4899];

        for (let level = 1; level <= 3; level++) {
            const sy = (h / 3.5) * level;
            const shelf = new THREE.Mesh(shelfGeo, shelfMat);
            shelf.position.y = sy;
            group.add(shelf);

            // Stored component boxes on each shelf
            const numBoxes = Math.floor(w / 1.4);
            for (let b = 0; b < numBoxes; b++) {
                const boxW = 1.0, boxH = 0.8, boxD = d * 0.75;
                const boxGeo = new THREE.BoxGeometry(boxW, boxH, boxD);
                const bCol = binColors[(b + level) % binColors.length];
                const boxMat = new THREE.MeshStandardMaterial({ color: bCol, roughness: 0.7 });
                const box = new THREE.Mesh(boxGeo, boxMat);
                box.position.set(-hw + 0.7 + b * 1.3, sy + boxH / 2 + 0.08, 0);
                group.add(box);
            }
        }

        group.position.set(x, 0, z);
        scene.add(group);
    }

    function createLiftUnit(x, z) {
        const geo = new THREE.BoxGeometry(5, 4.5, 4);
        const mat = new THREE.MeshStandardMaterial({ color: 0xF59E0B, metalness: 0.5 });
        const mesh = new THREE.Mesh(geo, mat);
        mesh.position.set(x, 2.25, z);
        scene.add(mesh);
    }

    function createHoistUnit(x, z) {
        const geo = new THREE.BoxGeometry(4, 5.0, 3.5);
        const mat = new THREE.MeshStandardMaterial({ color: 0xEF4444, metalness: 0.6 });
        const mesh = new THREE.Mesh(geo, mat);
        mesh.position.set(x, 2.5, z);
        scene.add(mesh);
    }

    // ─────────────────────────────────────────────────────────────
    // 5. HEAT INDUCTION AREA (Left-Center)
    // ─────────────────────────────────────────────────────────────
    function buildHeatInductionArea() {
        const positions = [
            [-58, 22], [-58, 14],
            [-50, 22], [-50, 14]
        ];
        positions.forEach(([x, z], idx) => {
            const group = new THREE.Group();
            // Machine cabinet
            const baseGeo = new THREE.BoxGeometry(4.5, 3.2, 4.0);
            const baseMat = new THREE.MeshStandardMaterial({ color: PALETTE.heatMachine, metalness: 0.6 });
            const base = new THREE.Mesh(baseGeo, baseMat);
            base.position.y = 1.6;
            group.add(base);

            // Induction coil / chamber
            const coilGeo = new THREE.CylinderGeometry(1.0, 1.0, 1.4, 16);
            const coilMat = new THREE.MeshStandardMaterial({ color: 0xF97316, emissive: 0xEA580C, emissiveIntensity: 0.3 });
            const coil = new THREE.Mesh(coilGeo, coilMat);
            coil.position.y = 3.6;
            group.add(coil);

            group.position.set(x, 0, z);
            scene.add(group);
        });

        addAreaLabel('HEAT INDUCTION', -54, 18);
    }

    // ─────────────────────────────────────────────────────────────
    // 6. PACKING AREA (Top Center)
    // ─────────────────────────────────────────────────────────────
    function buildPackingArea() {
        const packX = -26, packZ = -36;

        // Packing conveyors / tables
        for (let i = 0; i < 3; i++) {
            const tableGeo = new THREE.BoxGeometry(10, 1.4, 2.4);
            const tableMat = new THREE.MeshStandardMaterial({ color: 0x334155, metalness: 0.4 });
            const table = new THREE.Mesh(tableGeo, tableMat);
            table.position.set(packX + i * 12, 0.7, packZ);
            scene.add(table);

            // Finished goods stacked pallets (green)
            for (let p = 0; p < 2; p++) {
                const palGeo = new THREE.BoxGeometry(2.5, 1.8, 2.5);
                const palMat = new THREE.MeshStandardMaterial({ color: PALETTE.packingGreen, roughness: 0.6 });
                const pal = new THREE.Mesh(palGeo, palMat);
                pal.position.set(packX + i * 12 - 3 + p * 6, 0.9, packZ + 4.5);
                scene.add(pal);
            }
        }

        // OQC Inspection Station
        const oqcGroup = new THREE.Group();
        const oqcDeskGeo = new THREE.BoxGeometry(6, 1.4, 3);
        const oqcDeskMat = new THREE.MeshStandardMaterial({ color: 0x0284C7 });
        const desk = new THREE.Mesh(oqcDeskGeo, oqcDeskMat);
        desk.position.y = 0.7;
        oqcGroup.add(desk);

        // Inspection lamp
        const poleGeo = new THREE.CylinderGeometry(0.08, 0.08, 2.5);
        const poleMat = new THREE.MeshBasicMaterial({ color: 0xE2E8F0 });
        const pole = new THREE.Mesh(poleGeo, poleMat);
        pole.position.set(2.5, 2.0, 0);
        oqcGroup.add(pole);

        oqcGroup.position.set(12, 0, -36);
        scene.add(oqcGroup);

        addAreaLabel('PACKING & OQC', -15, -42);
    }

    // ─────────────────────────────────────────────────────────────
    // 7. FINAL ASSEMBLY: 11 ASSEMBLY LINES (The Main Focus)
    // ─────────────────────────────────────────────────────────────
    function buildFinalAssemblyLines() {
        // Line layout matching the CAD drawing:
        // Left Bank: Lines 1 - 6 (X: ~ -40 to -14)
        // Right Bank: Lines 7 - 11 (X: ~ -2 to +26)
        const leftBankLines = [
            { id: 1, name: 'Line 1', x: -28, z: -16, len: 24 },
            { id: 2, name: 'Line 2', x: -28, z: -8,  len: 24 },
            { id: 3, name: 'Line 3', x: -28, z: 0,   len: 24 },
            { id: 4, name: 'Line 4', x: -28, z: 8,   len: 24 },
            { id: 5, name: 'Line 5', x: -28, z: 16,  len: 24 },
            { id: 6, name: 'Line 6', x: -28, z: 24,  len: 24 }
        ];

        const rightBankLines = [
            { id: 7,  name: 'Line 7',  x: 12, z: -16, len: 24 },
            { id: 8,  name: 'Line 8',  x: 12, z: -8,  len: 24 },
            { id: 9,  name: 'Line 9',  x: 12, z: 0,   len: 24 },
            { id: 10, name: 'Line 10', x: 12, z: 8,   len: 24 },
            { id: 11, name: 'Line 11', x: 12, z: 16,  len: 24 }
        ];

        const allLines = [...leftBankLines, ...rightBankLines];

        allLines.forEach(lineConfig => {
            const lineObj = createConveyorLine(lineConfig);
            linesMap.set(lineConfig.id, lineObj);
        });

        addAreaLabel('FINAL ASSEMBLY (11 LINES)', -8, -21);
    }

    function createConveyorLine(cfg) {
        const group = new THREE.Group();
        group.name = 'assembly_line_' + cfg.id;
        group.userData = { lineId: cfg.id, name: cfg.name };

        const len = cfg.len;
        const width = 2.6;
        const height = 1.3;

        // 1. Conveyor Main Bed
        const bedGeo = new THREE.BoxGeometry(len, height, width);
        const bedMat = new THREE.MeshStandardMaterial({
            color: PALETTE.conveyorBelt,
            roughness: 0.6,
            metalness: 0.4
        });
        const bedMesh = new THREE.Mesh(bedGeo, bedMat);
        bedMesh.position.y = height / 2;
        bedMesh.castShadow = true;
        bedMesh.receiveShadow = true;
        group.add(bedMesh);

        // 2. Glowing Dynamic Status Edge Light (Changes Color!)
        const stripGeo = new THREE.BoxGeometry(len, 0.12, 0.12);
        const stripMat = new THREE.MeshBasicMaterial({
            color: PALETTE.statusGreen,
            transparent: true,
            opacity: 0.95
        });
        const glowStripFront = new THREE.Mesh(stripGeo, stripMat);
        glowStripFront.position.set(0, height + 0.06, width / 2);
        group.add(glowStripFront);

        const glowStripBack = new THREE.Mesh(stripGeo, stripMat.clone());
        glowStripBack.position.set(0, height + 0.06, -width / 2);
        group.add(glowStripBack);

        // 3. Worker Station Desks along the line (12 positions in CAD)
        const numStations = 8;
        const spacing = (len - 2) / numStations;
        for (let s = 0; s < numStations; s++) {
            const sx = -len / 2 + 1.2 + s * spacing;
            // Left seat
            createOperatorChair(sx, height, width / 2 + 0.7, group);
            // Right seat
            createOperatorChair(sx, height, -width / 2 - 0.7, group);
        }

        // 4. Overhead Lighting Rails
        const railMat = new THREE.MeshBasicMaterial({ color: 0x94A3B8 });
        const postMat = new THREE.MeshStandardMaterial({ color: 0x475569 });

        [-len / 2 + 1, len / 2 - 1].forEach(px => {
            const postGeo = new THREE.BoxGeometry(0.15, 2.2, 0.15);
            const p1 = new THREE.Mesh(postGeo, postMat);
            p1.position.set(px, height + 1.1, width / 2);
            group.add(p1);
            const p2 = new THREE.Mesh(postGeo, postMat);
            p2.position.set(px, height + 1.1, -width / 2);
            group.add(p2);
        });

        const topRailGeo = new THREE.BoxGeometry(len, 0.1, 0.1);
        const topRail1 = new THREE.Mesh(topRailGeo, railMat);
        topRail1.position.set(0, height + 2.2, width / 2);
        group.add(topRail1);
        const topRail2 = new THREE.Mesh(topRailGeo, railMat);
        topRail2.position.set(0, height + 2.2, -width / 2);
        group.add(topRail2);

        // 5. ANDON TOWER BEACON (Changes to Red/Yellow/Green with Pulse!)
        const andonGroup = new THREE.Group();
        const poleGeo = new THREE.CylinderGeometry(0.08, 0.08, 3.2, 12);
        const pole = new THREE.Mesh(poleGeo, new THREE.MeshStandardMaterial({ color: 0x334155 }));
        pole.position.y = 1.6;
        andonGroup.add(pole);

        // The glowing beacon bulb
        const bulbGeo = new THREE.SphereGeometry(0.5, 16, 16);
        const bulbMat = new THREE.MeshStandardMaterial({
            color: PALETTE.statusGreen,
            emissive: PALETTE.statusGreen,
            emissiveIntensity: 0.9,
            roughness: 0.2
        });
        const bulbMesh = new THREE.Mesh(bulbGeo, bulbMat);
        bulbMesh.position.y = 3.3;
        andonGroup.add(bulbMesh);

        // Local PointLight for dynamic ambient glow
        const andonLight = new THREE.PointLight(PALETTE.statusGreen, 0.8, 12);
        andonLight.position.y = 3.5;
        andonGroup.add(andonLight);

        // Floor pulse ring below andon tower
        const ringGeo = new THREE.RingGeometry(0.6, 1.4, 24);
        const ringMat = new THREE.MeshBasicMaterial({
            color: PALETTE.statusGreen,
            side: THREE.DoubleSide,
            transparent: true,
            opacity: 0.5
        });
        const ringMesh = new THREE.Mesh(ringGeo, ringMat);
        ringMesh.rotation.x = -Math.PI / 2;
        ringMesh.position.y = 0.08;
        andonGroup.add(ringMesh);

        andonGroup.position.set(len / 2 + 0.8, 0, 0);
        group.add(andonGroup);

        // 6. ANIMATED PRODUCTS (Audio unit chassis moving on the conveyor)
        const products = [];
        const numProd = 4;
        for (let p = 0; p < numProd; p++) {
            const prodGeo = new THREE.BoxGeometry(1.2, 0.45, 1.1);
            const prodMat = new THREE.MeshStandardMaterial({
                color: 0x0F172A, // Black audio amplifier unit
                metalness: 0.8,
                roughness: 0.3
            });
            const prodMesh = new THREE.Mesh(prodGeo, prodMat);
            // LED volume / power indicator on the front of amplifier
            const ledGeo = new THREE.BoxGeometry(0.2, 0.1, 0.05);
            const ledMat = new THREE.MeshBasicMaterial({ color: 0x00F0FF });
            const led = new THREE.Mesh(ledGeo, ledMat);
            led.position.set(0.3, 0.05, 0.56);
            prodMesh.add(led);

            const initialX = -len / 2 + 1.5 + (p * (len - 3) / numProd);
            prodMesh.position.set(initialX, height + 0.25, 0);
            group.add(prodMesh);

            products.push({
                mesh: prodMesh,
                minX: -len / 2 + 1.0,
                maxX: len / 2 - 1.0,
                speed: 0.045 + Math.random() * 0.015
            });
        }

        group.position.set(cfg.x, 0, cfg.z);
        scene.add(group);

        return {
            id: cfg.id,
            name: cfg.name,
            group: group,
            conveyorMesh: bedMesh,
            glowStrips: [glowStripFront, glowStripBack],
            bulbMesh: bulbMesh,
            andonLight: andonLight,
            floorRing: ringMesh,
            products: products,
            worldPos: new THREE.Vector3(cfg.x + len / 2 - 2, height + 1.2, cfg.z),
            data: { speed: 85, statusColor: 'green', actual: 0, plan: 0, dailyPlan: 0 }
        };
    }

    function createOperatorChair(x, y, z, parentGroup) {
        const chairGeo = new THREE.CylinderGeometry(0.3, 0.3, 0.5, 8);
        const chairMat = new THREE.MeshStandardMaterial({ color: 0x475569 });
        const chair = new THREE.Mesh(chairGeo, chairMat);
        chair.position.set(x, 0.25, z);
        parentGroup.add(chair);
    }

    // ─────────────────────────────────────────────────────────────
    // 8. PCB ASSEMBLY AREA (Top Right)
    // ─────────────────────────────────────────────────────────────
    function buildPCBAssemblyArea() {
        const pcbLines = [
            { name: 'AC/REF',    z: -42 },
            { name: 'RADIO/REF', z: -35 },
            { name: 'P SRS',     z: -28 },
            { name: 'D SRS',     z: -21 }
        ];

        pcbLines.forEach(line => {
            const startX = 22, endX = 64;
            const len = endX - startX;
            const x = startX + len / 2;

            // SMT Line Conveyor (Teal/Emerald)
            const geo = new THREE.BoxGeometry(len, 1.4, 2.0);
            const mat = new THREE.MeshStandardMaterial({ color: PALETTE.pcbMachine, metalness: 0.5 });
            const mesh = new THREE.Mesh(geo, mat);
            mesh.position.set(x, 0.7, line.z);
            scene.add(mesh);

            // Dipping & Fluxer Machines on the line
            createPCBMachine(startX + 4, line.z, 'FLUXER');
            createPCBMachine(startX + 14, line.z, 'DIP');
            createPCBMachine(endX - 6, line.z, 'REFLOW');
        });

        // Vertical WIP Test Racks on east edge of PCB
        for (let r = 0; r < 5; r++) {
            createPalletRack(67, -42 + r * 5.5, 2.2, 4.0, 4.2, PALETTE.pcbAccent);
        }

        addAreaLabel('PCB ASSEMBLY', 42, -45);
    }

    function createPCBMachine(x, z, type) {
        const group = new THREE.Group();
        const bodyGeo = new THREE.BoxGeometry(4.0, 3.2, 3.0);
        const bodyMat = new THREE.MeshStandardMaterial({ color: 0x0F766E, metalness: 0.7 });
        const body = new THREE.Mesh(bodyGeo, bodyMat);
        body.position.y = 1.6;
        group.add(body);

        // Display screen / chimney
        const topGeo = new THREE.BoxGeometry(1.6, 1.0, 1.6);
        const topMat = new THREE.MeshBasicMaterial({ color: 0x2DD4BF });
        const top = new THREE.Mesh(topGeo, topMat);
        top.position.y = 3.6;
        group.add(top);

        group.position.set(x, 0, z);
        scene.add(group);
    }

    // ─────────────────────────────────────────────────────────────
    // 9. SPECIALIZED WORKSHOPS (Bottom Wall)
    // ─────────────────────────────────────────────────────────────
    function buildSpecializedWorkshops() {
        // Solder Bar & IC Warehouse Room
        createGlassWall(15, 38, 16, 18, WALL_H * 0.85);
        createPalletRack(11, 40, 4, 2, 4.0, 0x0284C7);
        createPalletRack(19, 40, 4, 2, 4.0, 0x0284C7);
        addAreaLabel('SOLDER BAR & IC', 15, 42);

        // Production Engineering Workshop Room
        createGlassWall(40, 38, 22, 18, WALL_H * 0.85);
        // Heavy workbenches
        createBench(34, 38, 6, 3);
        createBench(46, 38, 6, 3);
        addAreaLabel('ENG WORKSHOP', 40, 42);

        // Pantry
        createGlassWall(60, 38, 12, 18, WALL_H * 0.85);
        addAreaLabel('PANTRY', 60, 42);
    }

    // ─────────────────────────────────────────────────────────────
    // 10. SOUND & TESTING ROOMS (Mid-Right)
    // ─────────────────────────────────────────────────────────────
    function buildSoundAndTestingRooms() {
        const soundX = 72, soundZ = 6;

        // Acoustic Wedge Walls (Anechoic Chamber Styling)
        const roomWallGeo = new THREE.BoxGeometry(14, WALL_H * 0.95, 24);
        const roomWallMat = new THREE.MeshStandardMaterial({
            color: PALETTE.soundAcoustic,
            roughness: 0.9,
            metalness: 0.1
        });
        const roomWall = new THREE.Mesh(roomWallGeo, roomWallMat);
        roomWall.position.set(soundX, (WALL_H * 0.95) / 2, soundZ);
        scene.add(roomWall);

        // Cutout opening inside
        const interiorGeo = new THREE.BoxGeometry(12, WALL_H * 0.96, 22);
        const interiorMat = new THREE.MeshBasicMaterial({ color: 0x0F172A });
        const interior = new THREE.Mesh(interiorGeo, interiorMat);
        interior.position.set(soundX, (WALL_H * 0.96) / 2, soundZ);
        scene.add(interior);

        // Shield Room (RF Copper mesh styling)
        const shieldGeo = new THREE.BoxGeometry(6, WALL_H * 0.9, 10);
        const shieldMat = new THREE.MeshStandardMaterial({
            color: 0xB45309,
            metalness: 0.9,
            roughness: 0.3
        });
        const shield = new THREE.Mesh(shieldGeo, shieldMat);
        shield.position.set(soundX - 11, (WALL_H * 0.9) / 2, soundZ - 6);
        scene.add(shield);

        addAreaLabel('SOUND ROOM', soundX, soundZ);
        addAreaLabel('SHIELD', soundX - 11, soundZ - 6);
    }

    // ─────────────────────────────────────────────────────────────
    // 11. OFFICES AREA (Far Right)
    // ─────────────────────────────────────────────────────────────
    function buildOfficesArea() {
        const offX = 89;

        // Glass room dividers
        createGlassWall(offX, -20, 20, 44, WALL_H * 0.9); // Main Office perimeter
        createGlassWall(offX, -38, 20, 0.8, WALL_H * 0.9); // Guest room divider
        createGlassWall(offX, -2, 20, 0.8, WALL_H * 0.9);  // Training divider

        // Office desk cubicles
        for (let r = 0; r < 3; r++) {
            for (let c = 0; c < 2; c++) {
                const dx = offX - 4 + c * 8;
                const dz = -28 + r * 7;
                createOfficeDesk(dx, dz);
            }
        }

        addAreaLabel('MAIN OFFICE', offX, -20);
        addAreaLabel('GUEST ROOM', offX, -42);
        addAreaLabel('TRAINING', offX - 14, -20);
    }

    function createOfficeDesk(x, z) {
        const deskGroup = new THREE.Group();
        const deskGeo = new THREE.BoxGeometry(4.5, 1.2, 2.2);
        const deskMat = new THREE.MeshStandardMaterial({ color: PALETTE.officeDesk, roughness: 0.5 });
        const desk = new THREE.Mesh(deskGeo, deskMat);
        desk.position.y = 0.6;
        deskGroup.add(desk);

        // Computer monitor
        const monGeo = new THREE.BoxGeometry(1.2, 0.8, 0.1);
        const monMat = new THREE.MeshBasicMaterial({ color: 0x38BDF8 });
        const mon = new THREE.Mesh(monGeo, monMat);
        mon.position.set(0, 1.5, 0.6);
        deskGroup.add(mon);

        deskGroup.position.set(x, 0, z);
        scene.add(deskGroup);
    }

    // ─────────────────────────────────────────────────────────────
    // 12. 3D HTML OVERLAYS & LINE MARKERS
    // ─────────────────────────────────────────────────────────────
    let overlayContainer;
    let tooltipEl;

    function createUIOverlays() {
        overlayContainer = document.createElement('div');
        overlayContainer.className = 'factory-3d-overlay';
        overlayContainer.style.cssText = `
            position: absolute;
            left: 0;
            top: 0;
            width: 100%;
            height: 100%;
            pointer-events: none;
            overflow: hidden;
            z-index: 10;
        `;
        container.appendChild(overlayContainer);

        // Tooltip element
        tooltipEl = document.createElement('div');
        tooltipEl.className = 'factory-3d-tooltip';
        tooltipEl.style.cssText = `
            position: absolute;
            display: none;
            background: rgba(15, 23, 42, 0.95);
            border: 1px solid rgba(255, 255, 255, 0.2);
            border-radius: 8px;
            padding: 10px 14px;
            color: #fff;
            font-size: 0.8rem;
            pointer-events: none;
            box-shadow: 0 8px 24px rgba(0,0,0,0.6);
            transform: translate(-50%, -120%);
            white-space: nowrap;
            z-index: 25;
            backdrop-filter: blur(8px);
        `;
        container.appendChild(tooltipEl);

        // Create 2D badges for each line
        linesMap.forEach(line => {
            const badge = document.createElement('div');
            badge.className = 'factory-line-badge';
            badge.dataset.lineId = line.id;
            badge.style.cssText = `
                position: absolute;
                transform: translate(-50%, -50%);
                padding: 1px 6px;
                border-radius: 6px;
                font-size: 0.68rem;
                font-weight: bold;
                color: #ffffff;
                background: #10B981;
                border: 1px solid rgba(255, 255, 255, 0.4);
                box-shadow: 0 2px 8px rgba(0,0,0,0.5);
                pointer-events: auto;
                cursor: pointer;
                transition: transform 0.2s, background 0.3s;
                white-space: nowrap;
                user-select: none;
            `;
            badge.textContent = `L${line.id}: 0%`;

            badge.addEventListener('mouseenter', () => showLineTooltip(line.id, badge));
            badge.addEventListener('mouseleave', hideLineTooltip);
            badge.addEventListener('click', () => focusOnLine(line.id));

            overlayContainer.appendChild(badge);
            lineMarkers2D.push({ id: line.id, element: badge, worldPos: line.worldPos });
        });

        // Add 3D Viewport Controls (Top Right of Map Card)
        createViewportControls();
    }

    function createViewportControls() {
        const controlsToolbar = document.createElement('div');
        controlsToolbar.style.cssText = `
            position: absolute;
            top: 12px;
            right: 14px;
            display: flex;
            gap: 6px;
            z-index: 20;
            background: rgba(15, 23, 42, 0.75);
            border: 1px solid rgba(255, 255, 255, 0.2);
            padding: 4px;
            border-radius: 20px;
            backdrop-filter: blur(6px);
        `;

        const btnStyle = `
            background: transparent;
            border: none;
            color: #E2E8F0;
            font-size: 0.75rem;
            padding: 4px 9px;
            border-radius: 14px;
            cursor: pointer;
            display: flex;
            align-items: center;
            gap: 4px;
            transition: background 0.2s;
        `;

        // 1. Isometric View Button
        const btnIso = document.createElement('button');
        btnIso.innerHTML = 'Iso';
        btnIso.style.cssText = btnStyle;
        btnIso.onclick = setViewIsometric;
        controlsToolbar.appendChild(btnIso);

        // 2. Top-down 2D View Button
        const btnTop = document.createElement('button');
        btnTop.innerHTML = '2D';
        btnTop.style.cssText = btnStyle;
        btnTop.onclick = setViewTopDown;
        controlsToolbar.appendChild(btnTop);

        // 3. Auto-Rotate Toggle Button
        const btnRotate = document.createElement('button');
        btnRotate.id = 'btn-toggle-rotate';
        btnRotate.innerHTML = 'Rotate';
        btnRotate.style.cssText = btnStyle;
        btnRotate.onclick = toggleAutoRotate;
        controlsToolbar.appendChild(btnRotate);

        // 4. Reset Button
        const btnReset = document.createElement('button');
        btnReset.innerHTML = 'Reset';
        btnReset.style.cssText = btnStyle;
        btnReset.onclick = resetCamera;
        controlsToolbar.appendChild(btnReset);

        container.appendChild(controlsToolbar);
    }

    function addAreaLabel(text, x, z) {
        areaLabels.push({ text: text, x: x, z: z });
    }

    // ─────────────────────────────────────────────────────────────
    // 13. DYNAMIC STATUS LOGIC & COLOR UPDATES (Green, Yellow, Red)
    // ─────────────────────────────────────────────────────────────
    function updateLineStatuses(statusList) {
        if (!statusList || !Array.isArray(statusList)) return;

        statusList.forEach(item => {
            const line = linesMap.get(item.lineNumber);
            if (!line) return;

            // Update stored data
            line.data = item;

            // Determine color from logic:
            // speed < 80 => Red (Critical)
            // speed < 85 => Yellow (Warning)
            // speed >= 85 => Green (Good)
            let hexColor = PALETTE.statusGreen;
            let cssColor = '#10B981';

            if (item.speed < 80.0) {
                hexColor = PALETTE.statusRed;
                cssColor = '#EF4444';
            } else if (item.speed < 85.0) {
                hexColor = PALETTE.statusYellow;
                cssColor = '#F59E0B';
            }

            // 1. Update Andon Bulb Mesh Emissive Color
            if (line.bulbMesh && line.bulbMesh.material) {
                line.bulbMesh.material.color.setHex(hexColor);
                line.bulbMesh.material.emissive.setHex(hexColor);
            }

            // 2. Update Andon PointLight
            if (line.andonLight) {
                line.andonLight.color.setHex(hexColor);
            }

            // 3. Update Floor Ring
            if (line.floorRing && line.floorRing.material) {
                line.floorRing.material.color.setHex(hexColor);
            }

            // 4. Update Glowing Side Strips
            if (line.glowStrips) {
                line.glowStrips.forEach(strip => {
                    if (strip && strip.material) {
                        strip.material.color.setHex(hexColor);
                    }
                });
            }

            // 5. Update 2D Floating Badge Text and Color
            const marker = lineMarkers2D.find(m => m.id === item.lineNumber);
            if (marker && marker.element) {
                marker.element.textContent = `L${item.lineNumber}: ${Math.round(item.speed)}%`;
                marker.element.style.background = cssColor;
                if (item.speed < 80.0) {
                    marker.element.style.animation = 'pulse-badge-red 1.2s infinite';
                } else {
                    marker.element.style.animation = 'none';
                }
            }
        });
    }

    // ─────────────────────────────────────────────────────────────
    // 14. INTERACTION & TOOLTIPS
    // ─────────────────────────────────────────────────────────────
    function onMouseMove(event) {
        const rect = renderer.domElement.getBoundingClientRect();
        mouse.x = ((event.clientX - rect.left) / rect.width) * 2 - 1;
        mouse.y = -((event.clientY - rect.top) / rect.height) * 2 + 1;

        raycaster.setFromCamera(mouse, camera);

        // Check intersection with conveyor lines
        const intersects = raycaster.intersectObjects(scene.children, true);
        let foundLine = null;

        for (let i = 0; i < intersects.length; i++) {
            let obj = intersects[i].object;
            while (obj && obj !== scene) {
                if (obj.userData && obj.userData.lineId) {
                    foundLine = obj.userData.lineId;
                    break;
                }
                obj = obj.parent;
            }
            if (foundLine) break;
        }

        if (foundLine) {
            renderer.domElement.style.cursor = 'pointer';
            if (hoveredLineId !== foundLine) {
                hoveredLineId = foundLine;
                showLineTooltipAtMouse(foundLine, event.clientX - rect.left, event.clientY - rect.top);
            } else {
                updateTooltipPosition(event.clientX - rect.left, event.clientY - rect.top);
            }
        } else {
            renderer.domElement.style.cursor = 'default';
            if (hoveredLineId !== null) {
                hoveredLineId = null;
                hideLineTooltip();
            }
        }
    }

    function onMouseClick(event) {
        if (hoveredLineId !== null) {
            focusOnLine(hoveredLineId);
        }
    }

    function showLineTooltip(lineId, badgeElement) {
        const line = linesMap.get(lineId);
        if (!line || !tooltipEl) return;
        const d = line.data || {};

        tooltipEl.innerHTML = `
            <div style="font-weight:bold; font-size:0.9rem; margin-bottom:4px; color:${d.speed < 80 ? '#EF4444' : (d.speed < 85 ? '#F59E0B' : '#10B981')};">
                ${d.name || 'Line ' + lineId} — ${d.statusText || 'Good'}
            </div>
            <div style="display:flex; justify-content:space-between; gap:16px; margin:2px 0;">
                <span style="color:#94A3B8;">Achievement Speed:</span>
                <b>${d.speed != null ? d.speed.toFixed(1) : '0'}%</b>
            </div>
            <div style="display:flex; justify-content:space-between; gap:16px; margin:2px 0;">
                <span style="color:#94A3B8;">Actual Output:</span>
                <b>${(d.actual || 0).toLocaleString('id-ID')} unit</b>
            </div>
            <div style="display:flex; justify-content:space-between; gap:16px; margin:2px 0;">
                <span style="color:#94A3B8;">Target Running:</span>
                <b>${(d.plan || 0).toLocaleString('id-ID')} unit</b>
            </div>
            <div style="display:flex; justify-content:space-between; gap:16px; margin:2px 0;">
                <span style="color:#94A3B8;">Daily Plan:</span>
                <b>${(d.dailyPlan || 0).toLocaleString('id-ID')} unit</b>
            </div>
        `;
        tooltipEl.style.display = 'block';

        if (badgeElement) {
            const rect = badgeElement.getBoundingClientRect();
            const parentRect = container.getBoundingClientRect();
            tooltipEl.style.left = (rect.left - parentRect.left + rect.width / 2) + 'px';
            tooltipEl.style.top = (rect.top - parentRect.top - 8) + 'px';
        }
    }

    function showLineTooltipAtMouse(lineId, x, y) {
        showLineTooltip(lineId, null);
        updateTooltipPosition(x, y);
    }

    function updateTooltipPosition(x, y) {
        if (!tooltipEl) return;
        tooltipEl.style.left = x + 'px';
        tooltipEl.style.top = (y - 12) + 'px';
    }

    function hideLineTooltip() {
        if (tooltipEl) tooltipEl.style.display = 'none';
    }

    function focusOnLine(lineId) {
        const line = linesMap.get(lineId);
        if (!line || !controls) return;
        const targetPos = line.worldPos;

        // Smooth tween camera target
        const startTarget = controls.target.clone();
        const endTarget = new THREE.Vector3(targetPos.x, 0, targetPos.z);
        const startTime = performance.now();
        const duration = 600;

        function tweenFocus(now) {
            const elapsed = now - startTime;
            const t = Math.min(elapsed / duration, 1);
            const ease = 0.5 - Math.cos(t * Math.PI) / 2; // in-out sine

            controls.target.lerpVectors(startTarget, endTarget, ease);
            controls.update();

            if (t < 1) {
                requestAnimationFrame(tweenFocus);
            }
        }
        requestAnimationFrame(tweenFocus);
    }

    // ─────────────────────────────────────────────────────────────
    // 15. CAMERA VIEW PRESETS
    // ─────────────────────────────────────────────────────────────
    function setViewIsometric() {
        if (!controls) return;
        controls.target.set(0, 0, 5);
        camera.position.set(0, 155, 130);
        controls.update();
    }

    function setViewTopDown() {
        if (!controls) return;
        controls.target.set(0, 0, 0);
        camera.position.set(0, 190, 0.1);
        controls.update();
    }

    function resetCamera() {
        setViewIsometric();
        isAutoRotating = false;
        updateRotateButtonUI();
    }

    function toggleAutoRotate() {
        isAutoRotating = !isAutoRotating;
        updateRotateButtonUI();
    }

    function updateRotateButtonUI() {
        const btn = document.getElementById('btn-toggle-rotate');
        if (btn) {
            btn.style.background = isAutoRotating ? 'rgba(59, 130, 246, 0.4)' : 'transparent';
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 16. THEME SUPPORT (Dark / Light)
    // ─────────────────────────────────────────────────────────────
    function setTheme(theme) {
        isLightMode = (theme === 'light');
        if (!scene) return;

        scene.background.setHex(isLightMode ? 0xE2E8F0 : 0x181B2D);
        if (floorMesh && floorMesh.material) {
            floorMesh.material.color.setHex(isLightMode ? PALETTE.floorLight : PALETTE.floorDark);
        }
        if (ambientLight) {
            ambientLight.intensity = isLightMode ? 0.9 : 0.65;
        }
        if (dirLight) {
            dirLight.intensity = isLightMode ? 0.95 : 0.7;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 17. MAIN ANIMATION & RENDER LOOP
    // ─────────────────────────────────────────────────────────────
    function animate() {
        animationFrameId = requestAnimationFrame(animate);

        pulseTime += 0.05;

        // Auto-rotation around factory center if active
        if (isAutoRotating && controls) {
            controls.autoRotate = true;
            controls.autoRotateSpeed = 1.0;
        } else if (controls) {
            controls.autoRotate = false;
        }

        if (controls) controls.update();

        // 1. Animate flowing products on each line
        linesMap.forEach(line => {
            if (line.products) {
                line.products.forEach(p => {
                    p.mesh.position.x += p.speed;
                    if (p.mesh.position.x > p.maxX) {
                        p.mesh.position.x = p.minX;
                    }
                });
            }

            // Pulse andon beacon bulb & floor ring for warning/critical lines
            if (line.data && line.data.speed < 85) {
                const pulse = (Math.sin(pulseTime * (line.data.speed < 80 ? 4.5 : 2.5)) + 1) / 2;
                if (line.bulbMesh && line.bulbMesh.material) {
                    line.bulbMesh.material.emissiveIntensity = 0.5 + pulse * 1.5;
                }
                if (line.andonLight) {
                    line.andonLight.intensity = 0.6 + pulse * 1.2;
                }
                if (line.floorRing) {
                    line.floorRing.scale.setScalar(1.0 + pulse * 0.35);
                    line.floorRing.material.opacity = 0.3 + pulse * 0.45;
                }
            } else {
                if (line.bulbMesh && line.bulbMesh.material) {
                    line.bulbMesh.material.emissiveIntensity = 0.8;
                }
                if (line.floorRing) {
                    line.floorRing.scale.setScalar(1.0);
                    line.floorRing.material.opacity = 0.4;
                }
            }
        });

        // 2. Project 3D positions of Line Badges to 2D Screen
        update2DMarkersPositions();

        // 3. Render
        renderer.render(scene, camera);
    }

    function update2DMarkersPositions() {
        if (!container || !camera) return;
        const widthHalf = container.clientWidth / 2;
        const heightHalf = container.clientHeight / 2;

        const tempVec = new THREE.Vector3();
        const positions = [];

        lineMarkers2D.forEach(marker => {
            if (!marker.element) return;
            tempVec.copy(marker.worldPos);
            tempVec.project(camera);

            if (tempVec.z > 1) {
                marker.element.style.display = 'none';
                return;
            }

            let x = (tempVec.x * widthHalf) + widthHalf;
            let y = -(tempVec.y * heightHalf) + heightHalf;

            positions.push({ marker, x, y });
        });

        // Anti-collision spacing pass: sort by Y and ensure minimum 20px separation for nearby badges
        positions.sort((a, b) => a.y - b.y);
        for (let i = 0; i < positions.length; i++) {
            for (let j = i + 1; j < positions.length; j++) {
                if (Math.abs(positions[j].x - positions[i].x) < 55) {
                    const diffY = positions[j].y - positions[i].y;
                    if (diffY < 20) {
                        positions[j].y = positions[i].y + 20;
                    }
                }
            }
        }

        // Clamp to container boundaries
        const padX = 25, padY = 20;
        positions.forEach(p => {
            const clampedX = Math.max(padX, Math.min(container.clientWidth - padX, p.x));
            const clampedY = Math.max(padY, Math.min(container.clientHeight - padY, p.y));
            p.marker.element.style.display = 'block';
            p.marker.element.style.left = `${Math.round(clampedX)}px`;
            p.marker.element.style.top = `${Math.round(clampedY)}px`;
        });
    }

    function onWindowResize() {
        if (!container || !renderer || !camera) return;
        const width = container.clientWidth;
        const height = container.clientHeight;
        if (width === 0 || height === 0) return;

        camera.aspect = width / height;
        camera.updateProjectionMatrix();
        renderer.setSize(width, height);
    }

    // Expose global interface
    window.AudioFactory3D = {
        init: initAudioFactory3D,
        updateLineStatuses: updateLineStatuses,
        setTheme: setTheme,
        setViewIsometric: setViewIsometric,
        setViewTopDown: setViewTopDown,
        resetCamera: resetCamera,
        toggleAutoRotate: toggleAutoRotate,
        focusOnLine: focusOnLine
    };

})(window);
