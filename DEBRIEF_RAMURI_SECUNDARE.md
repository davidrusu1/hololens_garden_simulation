# Debrief: cresterea ramurilor secundare

## Adresa proiectului si starea analizata

- Repository local: `/Users/alex/Documents/ICI internship/hololens_garden_simulation`
- Repository GitHub: `https://github.com/davidrusu1/hololens_garden_simulation.git`
- Branch: `horia_branch`
- Commit analizat: `079d454426ba7bc86a9fb6f32c404549bb370cf7` (`Added branch implementation`)
- Unity: `2022.3.62f2`
- Scena demonstrativa: `Assets/Main.unity`
- Modulul principal: `Assets/Scripts/Branch.cs`
- Prefab ramura: `Assets/Prefabs/Branch.prefab`
- Prefab punct de ramificare: `Assets/Prefabs/Joint.prefab`

Aceste coordonate sunt punctul de pornire pentru un alt agent. Inainte de implementare trebuie verificat ca repository-ul este tot pe `horia_branch` si ca modificarile locale generate de Unity nu sunt suprascrise accidental.

## Arhitectura existenta

`Branch` este un component Unity atasat prefabului `Assets/Prefabs/Branch.prefab`. Acelasi prefab este folosit atat pentru tulpina initiala, cat si recursiv pentru fiecare ramura secundara.

Prefab-ul contine:

- obiectul radacina `Branch`, care are componenta `Branch.cs`;
- copilul `Cylinder`, referit prin `modelCyl`, care reprezinta geometria vizibila;
- o autoreferinta `branchPrefab` catre acelasi prefab;
- o referinta `jointPrefab` catre un GameObject gol folosit ca pivot pentru orientarea ramurii copil.

`Plant.cs` si `Leaf.cs` nu participa momentan la crestere; ambele componente sunt goale.

## Fluxul de crestere intentionat

1. In `Start()`, ramura de generatie zero (`currGen == 0`) primeste `init = true` si `readyToGrow = true`.
2. Cilindrul este resetat la scara verticala zero.
3. In fiecare `Update()`, daca ramura este initializata, pregatita si nefinalizata, `localScale.y` creste cu `vitezaCrestere * Time.deltaTime`.
4. Centrul cilindrului este mutat la `localPosition.y = localScale.y`. Cum cilindrul Unity are inaltimea de baza 2, aceasta mentine baza ramurii la y = 0.
5. Cand `localScale.y` ajunge la `maxHeight`, ramura este marcata `maxed = true`.
6. Daca `currGen < maxGen`, porneste `GenJoints(2)`. Valoarea 2 este hardcodata si inseamna doua ramuri copil pentru fiecare ramura eligibila.
7. Pentru fiecare copil se alege aleator:
   - inaltimea pivotului: intre `0.3` si `2 * maxHeight - 0.3`;
   - azimutul: intre 0 si 360 de grade;
   - inclinarea este fixa la 45 de grade.
8. Se instantiaza un `Joint` ca fiu al ramurii curente, apoi o noua instanta `Branch` ca fiu al pivotului.
9. Copilul primeste generatia parintelui plus unu, `maxGen` si `vitezaCrestere`.
10. Corutina asteapta ca acel copil sa devina `maxed`, apoi mai asteapta `delayBranches` secunde si trece la urmatorul copil.

Cu `maxGen = 2` si doua ramuri per nod, intentia este o structura cu maximum 1 + 2 + 4 = 7 ramuri: tulpina principala, doua ramuri de generatia 1 si patru ramuri de generatia 2.

## Parametri existenti

| Parametru | Valoare prefab | Valoare in scena | Rol |
|---|---:|---:|---|
| `jointPrefab` | `Assets/Prefabs/Joint.prefab` | aceeasi referinta | Pivotul creat pe parinte pentru pozitie si rotatie. |
| `branchPrefab` | autoreferinta la `Branch.prefab` | mostenita | Prefabul instantiat recursiv pentru copil. |
| `modelCyl` | copilul `Cylinder` | mostenit | Transformul a carui scara simuleaza cresterea. |
| `delayBranches` | `1.0 s` | `2.0 s` doar pe ramura principala | Pauza dupa finalizarea unui copil si inaintea urmatorului. Nu intarzie primul copil. |
| `vitezaCrestere` | `0.5 unitati-scara/s` | mostenita | Viteza cu care creste `localScale.y`. Este copiata la copii. |
| `maxHeight` | `1.0` | mostenita | Limita pentru `localScale.y`; inaltimea geometrica rezultata este aproximativ `2 * maxHeight`. |
| `maxed` | `false` | mostenita | Stare interna: cresterea ramurii s-a terminat. |
| `init` | `false` | mostenita | Stare interna: ramura poate intra in logica de crestere. |
| `readyToGrow` | `false` | mostenita | Poarta suplimentara verificata in `Update()`. |
| `currGen` | `0` | mostenita | Generatia curenta; tulpina este 0. |
| `maxGen` | `2` | mostenita | Ultima generatie permisa. |
| `nrJoints` | `2`, hardcodat | `2` | Numarul de copii generati de fiecare ramura eligibila. |
| unghi inclinare | `45 deg`, hardcodat | `45 deg` | Inclinarea fiecarui pivot fata de ramura parinte. |
| interval azimut | `0..360 deg`, hardcodat | aleator | Directia ramurii in jurul axei parintelui. |
| margine verticala | `0.3`, hardcodata | `0.3` | Evita plasarea pivotului exact la baza sau varf. |

La valorile implicite, o ramura ajunge la limita in aproximativ `maxHeight / vitezaCrestere = 2 secunde`.

## Comportamentul efectiv si problemele gasite

### Blocaj critic al ramurilor secundare

In `GenJoints()`, copilul primeste `init = true`, dar nu primeste `readyToGrow = true`. Prefabul are `readyToGrow = false`, iar `Start()` activeaza automat aceasta stare doar pentru `currGen == 0`. Din acest motiv, copilul de generatie 1 se opreste la garda din `Update()` si nu devine niciodata `maxed`.

Consecinta: pivotul si prima ramura secundara sunt create, dar ramura ramane la inaltime zero, iar corutina parintelui ramane permanent in bucla `while (!newScript.maxed)`.

Remediul minim este setarea explicita:

```csharp
newScript.init = true;
newScript.readyToGrow = true;
```

O varianta mai robusta este o metoda publica `Initialize(...)` care seteaza atomic toti parametrii si toate starile copilului, inainte de primul sau `Update()`.

### Grosimea configurata in prefab este pierduta

Copilul `Cylinder` are initial `localScale.x/z = 0.2`, dar `Start()` il reseteaza la `(1, 0, 1)`, iar finalizarea la `(1, maxHeight, 1)`. Astfel, ramurile devin mult mai groase decat configuratia vizuala din prefab.

Agentul trebuie sa retina scara radiala initiala si sa modifice doar componenta y.

### Parametrii nu sunt propagati complet

Copiii primesc doar `maxGen` si `vitezaCrestere`. `maxHeight` si `delayBranches` raman valorile prefabului. In scena, tulpina are `delayBranches = 2`, dar copiii folosesc `1`.

Trebuie decis explicit daca acesti parametri sunt globali, mosteniti sau modificati pe generatie. Pentru comportament uniform, trebuie propagati toti parametrii de configurare.

### Ordinea este seriala

Parintele asteapta terminarea completa a fiecarui copil inainte sa creeze urmatorul copil. Aceasta produce crestere secventiala, nu simultana. Intre timp, copilul finalizat isi poate porni propria corutina, deci generatiile se pot suprapune partial.

Daca cerinta este ca ramurile surori sa creasca simultan, toate trebuie create mai intai, apoi asteptate separat sau lasate sa ruleze independent.

### Alte riscuri

- `Debug.Log("intru aici: " + currGen)` ruleaza in fiecare frame pentru fiecare ramura si trebuie eliminat sau protejat printr-un flag de debug, mai ales pentru HoloLens.
- `Random.Range(0.3f, realHeight - 0.3f)` necesita o inaltime geometrica suficienta; pentru `maxHeight <= 0.3`, intervalul nu mai este valid semantic.
- `nrJoints`, unghiul de 45 de grade si marginea 0.3 sunt hardcodate, deci nu pot fi reglate din Inspector.
- `readyToGrow = true` setat dupa `maxed = true` nu mai produce efect asupra ramurii curente, deoarece garda din `Update()` verifica `maxed`.
- Numarul total de ramuri creste exponential: pentru `b` copii si `g = maxGen`, totalul este `1 + b + ... + b^g`. Sunt necesare limite prudente pentru HoloLens.

## Implementare recomandata pentru continuare

1. Extinderea configuratiei serializate cu `branchesPerNode`, `branchAngle`, `spawnMargin`, eventual intervale pentru lungime si unghi.
2. Inlocuirea flagurilor publice de stare cu proprietati private sau read-only; configuratia ramane editabila in Inspector prin `[SerializeField]`.
3. Adaugarea metodei `Initialize(int generation, BranchSettings settings)` sau a unei configuratii echivalente, care seteaza toate valorile si activeaza cresterea copilului.
4. Pastrarea razei initiale a cilindrului si modificarea exclusiva a scalei y.
5. Validarea parametrilor in `OnValidate()`: viteza pozitiva, inaltime pozitiva, `maxGen >= 0`, margine mai mica decat jumatate din inaltimea reala.
6. Alegerea explicita a modelului temporal:
   - serial: copilul urmator apare dupa ce precedentul a crescut si dupa delay;
   - paralel: toate ramurile surori apar/cresc impreuna;
   - esalonat: copiii apar la intervale, fara a astepta finalizarea celui anterior.
7. Separarea configuratiei de starea runtime, ideal printr-un `ScriptableObject` sau un component coordonator pe `Plant`, daca simularea va avea mai multe specii ori faze de crestere.
8. Eliminarea logarii per-frame si verificarea profilerului pe dispozitivul tinta.

## Pseudocod pentru remediul minim

```text
la Start:
  memoreaza raza initiala a cilindrului
  reseteaza numai scala Y
  daca generatia este 0, porneste cresterea

la Update:
  daca ramura nu este activa sau este finalizata, iesi
  creste scala Y cu viteza * deltaTime
  muta centrul la scalaY pentru a pastra baza fixa
  cand ajunge la limita:
    marcheaza finalizata
    daca mai sunt generatii, porneste generarea copiilor

la crearea copilului:
  instantiaza pivotul la o inaltime si un azimut valide
  instantiaza prefab-ul ramurii sub pivot
  transmite generatia, limitele si toti parametrii
  seteaza explicit copilul activ pentru crestere
```

## Criterii de acceptare

- Tulpina principala creste de la baza fara schimbarea grosimii configurate.
- Prima ramura secundara devine vizibila si ajunge la inaltimea tinta.
- Pentru `maxGen = 2` si `branchesPerNode = 2` se pot obtine maximum 7 ramuri.
- Nicio corutina nu ramane blocata asteptand un copil care nu poate porni.
- Modificarea vitezei, inaltimii, numarului de copii, unghiului si delay-ului din Inspector produce efectul asteptat.
- `maxGen = 0` produce doar tulpina principala.
- Configuratiile cu inaltime mica nu genereaza intervale aleatoare invalide.
- Consola nu este inundata cu mesaje in fiecare frame.
- Comportamentul este verificat in `Assets/Main.unity`, apoi in scena Mixed Reality reala, fara inlocuirea infrastructurii XR existente.

## Fisiere care nu trebuie confundate

`Assets/MRTemplateAssets/Prefabs/Branch.prefab` este un prefab vechi bazat pe `LineRenderer` si nu contine componenta `Branch.cs`. Implementarea analizata foloseste `Assets/Prefabs/Branch.prefab`.
