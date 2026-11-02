/* ==========================================================================
   THREE.JS MODULE 2: MÔ PHỎNG 3D LY ĐỒ UỐNG & BÁNH NGỌT TƯƠNG TÁC (/product/{id})
   Thay đổi trực tiếp theo Size (S/M/L), Nóng/Lạnh, Mức đá (0-100%), Topping 3D
   ========================================================================== */
import * as THREE from '../lib/three/three.module.min.js';
import { OrbitControls } from '../lib/three/OrbitControls.js';

export function initDrinkCustomizer3D(containerId, initialConfig) {
    const container = document.getElementById(containerId);
    if (!container) return null;

    const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    const state = {
        isBakery: !!initialConfig.isBakery,
        liquidColor: initialConfig.liquidColor || '#6F4E37',
        sizeName: initialConfig.sizeName || 'M',
        temperature: initialConfig.temperature || 'Lạnh',
        icePercent: parseInt(initialConfig.iceLevel || '100', 10) || 100,
        toppings: initialConfig.toppings || [] // mảng tên topping đang chọn
    };

    const scene = new THREE.Scene();

    const camera = new THREE.PerspectiveCamera(38, container.clientWidth / container.clientHeight, 0.1, 50);
    camera.position.set(0, 2.3, 5.2);

    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true });
    renderer.setSize(container.clientWidth, container.clientHeight);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.shadowMap.enabled = true;
    container.appendChild(renderer.domElement);

    const controls = new OrbitControls(camera, renderer.domElement);
    controls.enableDamping = true;
    controls.enablePan = false;
    controls.minDistance = 3.2;
    controls.maxDistance = 7.5;
    controls.maxPolarAngle = Math.PI / 1.95;
    controls.target.set(0, 1.05, 0);
    controls.autoRotate = !prefersReducedMotion;
    controls.autoRotateSpeed = 1.1;

    // Ánh sáng studio sản phẩm F&B
    scene.add(new THREE.AmbientLight(0xfff7ed, 1.1));

    const keyLight = new THREE.DirectionalLight(0xffffff, 1.6);
    keyLight.position.set(4, 7, 5);
    keyLight.castShadow = true;
    scene.add(keyLight);

    const rimLight = new THREE.PointLight(0xffb36b, 1.2, 12);
    rimLight.position.set(-3.5, 3, -3);
    scene.add(rimLight);

    // Đế gỗ bàn trưng bày
    const coaster = new THREE.Mesh(
        new THREE.CylinderGeometry(1.35, 1.45, 0.14, 36),
        new THREE.MeshStandardMaterial({ color: 0x5c3c24, roughness: 0.65 })
    );
    coaster.position.y = -0.07;
    coaster.receiveShadow = true;
    scene.add(coaster);

    const modelGroup = new THREE.Group();
    scene.add(modelGroup);

    let steamPuffs = [];
    let iceMeshes = [];
    let targetScale = new THREE.Vector3(1, 1, 1);

    function rebuildModel() {
        while (modelGroup.children.length > 0) {
            modelGroup.remove(modelGroup.children[0]);
        }
        steamPuffs = [];
        iceMeshes = [];

        // Tỉ lệ theo Size S / M / L hoặc Combo
        const sLower = (state.sizeName || 'M').toUpperCase();
        if (sLower === 'S') targetScale.set(0.88, 0.84, 0.88);
        else if (sLower === 'L' || sLower.includes('COMBO')) targetScale.set(1.12, 1.18, 1.12);
        else targetScale.set(1.0, 1.0, 1.0);

        if (state.isBakery) {
            buildBakeryPlate();
            return;
        }

        const isHot = (state.temperature || '').toLowerCase().includes('nóng');
        if (isHot) {
            buildHotMug();
        } else {
            buildColdGlass();
        }
    }

    function buildBakeryPlate() {
        const plate = new THREE.Mesh(
            new THREE.CylinderGeometry(1.18, 0.9, 0.12, 32),
            new THREE.MeshStandardMaterial({ color: 0xfffdf9, roughness: 0.25 })
        );
        plate.position.y = 0.08;
        plate.castShadow = true;
        modelGroup.add(plate);

        const cakeMat = new THREE.MeshStandardMaterial({ color: new THREE.Color(state.liquidColor), roughness: 0.58 });
        const cakeBase = new THREE.Mesh(new THREE.CylinderGeometry(0.68, 0.72, 0.56, 28), cakeMat);
        cakeBase.position.y = 0.42;
        cakeBase.castShadow = true;
        modelGroup.add(cakeBase);

        const creamTop = new THREE.Mesh(
            new THREE.CylinderGeometry(0.65, 0.68, 0.14, 28),
            new THREE.MeshStandardMaterial({ color: 0xfff8e7, roughness: 0.4 })
        );
        creamTop.position.y = 0.75;
        modelGroup.add(creamTop);

        const garnish = new THREE.Mesh(
            new THREE.SphereGeometry(0.13, 16, 16),
            new THREE.MeshStandardMaterial({ color: 0xdc2626, roughness: 0.3 })
        );
        garnish.position.set(0, 0.88, 0);
        modelGroup.add(garnish);
    }

    function buildHotMug() {
        const ceramicMat = new THREE.MeshStandardMaterial({ color: 0xf7efe6, roughness: 0.28 });
        const mugBody = new THREE.Mesh(new THREE.CylinderGeometry(0.78, 0.62, 1.65, 32, 1, true), ceramicMat);
        mugBody.material.side = THREE.DoubleSide;
        mugBody.position.y = 0.84;
        mugBody.castShadow = true;
        modelGroup.add(mugBody);

        const mugBottom = new THREE.Mesh(new THREE.CircleGeometry(0.62, 32), ceramicMat);
        mugBottom.rotation.x = -Math.PI / 2;
        mugBottom.position.y = 0.02;
        modelGroup.add(mugBottom);

        // Đai thương hiệu Đạt Mộc
        const sleeve = new THREE.Mesh(
            new THREE.CylinderGeometry(0.73, 0.68, 0.52, 32),
            new THREE.MeshStandardMaterial({ color: 0x6f4e37, roughness: 0.75 })
        );
        sleeve.position.y = 0.82;
        modelGroup.add(sleeve);

        // Quai cốc sứ
        const handle = new THREE.Mesh(new THREE.TorusGeometry(0.38, 0.08, 14, 28, Math.PI), ceramicMat);
        handle.rotation.z = -Math.PI / 2;
        handle.position.set(0.71, 0.88, 0);
        modelGroup.add(handle);

        // Bề mặt cà phê / đồ uống nóng
        const hasExtraEspresso = state.toppings.some(t => t.toLowerCase().includes('espresso'));
        const baseCol = new THREE.Color(state.liquidColor);
        if (hasExtraEspresso) baseCol.multiplyScalar(0.72);

        const liquid = new THREE.Mesh(
            new THREE.CylinderGeometry(0.74, 0.61, 1.42, 32),
            new THREE.MeshStandardMaterial({ color: baseCol, roughness: 0.22 })
        );
        liquid.position.y = 0.74;
        modelGroup.add(liquid);

        // Lớp foam Macchiato nếu chọn topping kem
        const hasFoam = state.toppings.some(t => t.toLowerCase().includes('kem') || t.toLowerCase().includes('macchiato'));
        if (hasFoam) {
            const foam = new THREE.Mesh(
                new THREE.CylinderGeometry(0.75, 0.74, 0.2, 32),
                new THREE.MeshStandardMaterial({ color: 0xfffbeb, roughness: 0.5 })
            );
            foam.position.y = 1.5;
            modelGroup.add(foam);
        }

        // Hạt khói nóng 3D bốc lên
        for (let i = 0; i < 6; i++) {
            const puff = new THREE.Mesh(
                new THREE.SphereGeometry(0.11 + (i % 2) * 0.04, 12, 12),
                new THREE.MeshBasicMaterial({ color: 0xffffff, transparent: true, opacity: 0.35 })
            );
            puff.position.set((Math.random() - 0.5) * 0.45, 1.7 + i * 0.18, (Math.random() - 0.5) * 0.45);
            modelGroup.add(puff);
            steamPuffs.push({ mesh: puff, seed: i * 1.1 });
        }
    }

    function buildColdGlass() {
        // Ly thủy tinh trong suốt
        const glassMat = new THREE.MeshPhysicalMaterial({
            color: 0xffffff,
            transparent: true,
            opacity: 0.28,
            roughness: 0.08,
            metalness: 0.05,
            side: THREE.DoubleSide
        });

        const cupOuter = new THREE.Mesh(new THREE.CylinderGeometry(0.84, 0.58, 2.08, 36, 1, true), glassMat);
        cupOuter.position.y = 1.05;
        cupOuter.castShadow = true;
        modelGroup.add(cupOuter);

        const rim = new THREE.Mesh(
            new THREE.TorusGeometry(0.84, 0.025, 12, 36),
            new THREE.MeshStandardMaterial({ color: 0xffffff, transparent: true, opacity: 0.65 })
        );
        rim.rotation.x = Math.PI / 2;
        rim.position.y = 2.09;
        modelGroup.add(rim);

        // Chất lỏng bên trong ly
        const hasExtraEspresso = state.toppings.some(t => t.toLowerCase().includes('espresso'));
        const hasOatmilk = state.toppings.some(t => t.toLowerCase().includes('yến mạch') || t.toLowerCase().includes('oat'));
        const liqColor = new THREE.Color(state.liquidColor);
        if (hasExtraEspresso) liqColor.multiplyScalar(0.72);
        if (hasOatmilk) liqColor.lerp(new THREE.Color(0xf3e5d0), 0.32);

        const liquidMesh = new THREE.Mesh(
            new THREE.CylinderGeometry(0.77, 0.55, 1.72, 32),
            new THREE.MeshStandardMaterial({ color: liqColor, roughness: 0.2, transparent: true, opacity: 0.9 })
        );
        liquidMesh.position.y = 0.9;
        modelGroup.add(liquidMesh);

        // Ống hút giấy thân thiện môi trường
        const straw = new THREE.Mesh(
            new THREE.CylinderGeometry(0.055, 0.055, 2.55, 16),
            new THREE.MeshStandardMaterial({ color: 0x3b2314, roughness: 0.5 })
        );
        straw.position.set(0.24, 1.38, 0.1);
        straw.rotation.z = -0.14;
        modelGroup.add(straw);

        // Đá viên 3D nổi theo đúng tỉ lệ 0 / 30 / 50 / 70 / 100%
        const iceCount = Math.round((Math.max(0, Math.min(100, state.icePercent)) / 100) * 11);
        const iceMat = new THREE.MeshPhysicalMaterial({
            color: 0xe8f7ff,
            transparent: true,
            opacity: 0.68,
            roughness: 0.12
        });

        for (let i = 0; i < iceCount; i++) {
            const cube = new THREE.Mesh(new THREE.BoxGeometry(0.24, 0.22, 0.24), iceMat);
            const angle = (i / Math.max(1, iceCount)) * Math.PI * 2 + (i % 2) * 0.4;
            const r = (i % 3 === 0) ? 0.14 : 0.38;
            const yPos = 1.56 - Math.floor(i / 5) * 0.24;
            cube.position.set(Math.cos(angle) * r, yPos, Math.sin(angle) * r);
            cube.rotation.set(i * 0.5, i * 0.8, i * 0.3);
            modelGroup.add(cube);
            iceMeshes.push({ mesh: cube, baseY: yPos, phase: i * 0.9 });
        }

        // Topping Trân châu đen / Trân châu trắng / Thạch dưới đáy ly
        const hasBlackBoba = state.toppings.some(t => t.toLowerCase().includes('trân châu đen'));
        const hasWhiteBoba = state.toppings.some(t => t.toLowerCase().includes('trân châu trắng'));
        const hasJelly = state.toppings.some(t => t.toLowerCase().includes('thạch'));

        if (hasBlackBoba || hasWhiteBoba) {
            const pearlsCount = 22;
            for (let p = 0; p < pearlsCount; p++) {
                const isWhite = hasWhiteBoba && (!hasBlackBoba || p % 2 === 1);
                const pearl = new THREE.Mesh(
                    new THREE.SphereGeometry(0.082, 12, 12),
                    new THREE.MeshStandardMaterial({
                        color: isWhite ? 0xfff8eb : 0x1a0f0a,
                        roughness: 0.15,
                        transparent: isWhite,
                        opacity: isWhite ? 0.85 : 1.0
                    })
                );
                const a = p * 1.45;
                const rad = 0.12 + (p % 4) * 0.11;
                pearl.position.set(Math.cos(a) * rad, 0.14 + Math.floor(p / 9) * 0.13, Math.sin(a) * rad);
                modelGroup.add(pearl);
            }
        }

        if (hasJelly) {
            const jellyMat = new THREE.MeshStandardMaterial({ color: 0x3b1e0e, roughness: 0.2 });
            for (let j = 0; j < 10; j++) {
                const jCube = new THREE.Mesh(new THREE.BoxGeometry(0.13, 0.11, 0.13), jellyMat);
                const a = j * 0.9;
                jCube.position.set(Math.cos(a) * 0.32, 0.32 + (j % 2) * 0.12, Math.sin(a) * 0.32);
                jCube.rotation.set(j, j * 0.4, 0);
                modelGroup.add(jCube);
            }
        }

        // Topping Đào miếng nổi phía trên
        const hasPeach = state.toppings.some(t => t.toLowerCase().includes('đào'));
        if (hasPeach) {
            const peachMat = new THREE.MeshStandardMaterial({ color: 0xf59e0b, roughness: 0.35 });
            for (let k = 0; k < 3; k++) {
                const wedge = new THREE.Mesh(new THREE.CylinderGeometry(0.22, 0.22, 0.08, 12, 1, false, 0, Math.PI * 0.8), peachMat);
                const a = (k / 3) * Math.PI * 2;
                wedge.position.set(Math.cos(a) * 0.35, 1.72, Math.sin(a) * 0.35);
                wedge.rotation.set(0.4, a, 0.3);
                modelGroup.add(wedge);
            }
        }

        // Topping Kem phô mai Macchiato phủ trên miệng ly
        const hasFoam = state.toppings.some(t => t.toLowerCase().includes('kem') || t.toLowerCase().includes('macchiato'));
        if (hasFoam) {
            const foam = new THREE.Mesh(
                new THREE.CylinderGeometry(0.8, 0.77, 0.24, 32),
                new THREE.MeshStandardMaterial({ color: 0xfffbeb, roughness: 0.48 })
            );
            foam.position.y = 1.86;
            modelGroup.add(foam);
        }
    }

    rebuildModel();

    const clock = new THREE.Clock();
    function animate() {
        requestAnimationFrame(animate);
        const t = clock.getElapsedTime();

        modelGroup.scale.lerp(targetScale, 0.1);

        if (!prefersReducedMotion) {
            steamPuffs.forEach(p => {
                const c = (t * 0.45 + p.seed) % 1;
                p.mesh.position.y = 1.62 + c * 0.75;
                p.mesh.material.opacity = Math.sin(c * Math.PI) * 0.42;
            });

            iceMeshes.forEach(ic => {
                ic.mesh.position.y = ic.baseY + Math.sin(t * 1.8 + ic.phase) * 0.025;
            });
        }

        controls.update();
        renderer.render(scene, camera);
    }
    animate();

    window.addEventListener('resize', () => {
        if (!container.clientWidth || !container.clientHeight) return;
        camera.aspect = container.clientWidth / container.clientHeight;
        camera.updateProjectionMatrix();
        renderer.setSize(container.clientWidth, container.clientHeight);
    });

    return {
        updateCustomization(patch) {
            Object.assign(state, patch);
            rebuildModel();
        }
    };
}

