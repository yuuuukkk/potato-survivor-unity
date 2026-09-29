import assert from 'node:assert/strict';

// Isolated design spike: a test fixture stands in for the AI provider.
// This validates the proposal -> local guardrails -> shop purchase -> weapon
// behavior loop. It does not claim to validate a live model or Unity runtime.

const catalog = Object.freeze({
  sword_thrust: Object.freeze({
    id: 'sword_thrust',
    name: '磨锋：直刺',
    weaponId: 'sword',
    allowedKinds: ['Melee'],
    exclusiveGroup: 'sword_attack_form',
    form: 'Thrust',
    cost: 4,
    tradeoff: '放弃宽幅横扫，改为集中向前刺击。',
  }),
});

function createRun() {
  return {
    character: { id: 'farmer', allowedKinds: ['Projectile', 'Melee', 'Orbit', 'Turret', 'Boomerang'] },
    materials: 10,
    shopSlots: 3,
    weapons: [{ id: 'sword', baseForm: 'Swing', form: 'Swing', appliedMods: [] }],
  };
}

// Test fixture deliberately emits only an allowed local definition ID.
function fixtureAdvisor(intent, context) {
  assert.match(intent, /群怪|成群/);
  assert.equal(context.weapons.some(weapon => weapon.id === 'sword'), true);
  return { candidateIds: ['sword_thrust'], explanation: '把横扫改为向前刺击。' };
}

function validateProposal(run, proposal) {
  if (!proposal || !Array.isArray(proposal.candidateIds)) return { ok: false, error: 'invalid schema' };
  const accepted = [];
  for (const id of proposal.candidateIds) {
    const mod = catalog[id];
    if (!mod) return { ok: false, error: `unknown modification: ${id}` };
    const weapon = run.weapons.find(candidate => candidate.id === mod.weaponId);
    if (!weapon) return { ok: false, error: `weapon not owned: ${mod.weaponId}` };
    if (!run.character.allowedKinds.includes(mod.allowedKinds[0])) return { ok: false, error: 'character cannot use this modification' };
    if (weapon.appliedMods.some(owned => catalog[owned]?.exclusiveGroup === mod.exclusiveGroup))
      return { ok: false, error: 'conflicting modification' };
    accepted.push(mod);
  }
  return { ok: true, accepted };
}

function buyModification(run, mod) {
  assert.equal(run.shopSlots, 3, 'forge offer uses an existing slot; it does not add a fourth');
  if (!run.weapons.some(weapon => weapon.id === mod.weaponId)) return false;
  if (!run.character.allowedKinds.includes(mod.allowedKinds[0])) return false;
  if (run.materials < mod.cost) return false;
  const weapon = run.weapons.find(candidate => candidate.id === mod.weaponId);
  if (weapon.appliedMods.some(owned => catalog[owned]?.exclusiveGroup === mod.exclusiveGroup)) return false;
  run.materials -= mod.cost;
  weapon.appliedMods.push(mod.id);
  weapon.form = mod.form;
  return true;
}

function resolveAttackForm(weapon) {
  return weapon.form === 'Thrust'
    ? { label: 'forward_thrust', coverage: 'narrow_line', direction: 'toward_target' }
    : { label: 'outward_sweep', coverage: 'wide_arc', direction: 'sweeping_arc' };
}

function runClosedLoop() {
  const run = createRun();
  const intent = '我想让土豆剑更适合打成群的怪';
  const proposal = fixtureAdvisor(intent, { weapons: run.weapons });
  const checked = validateProposal(run, proposal);
  assert.equal(checked.ok, true);
  assert.equal(checked.accepted.length, 1);
  const mod = checked.accepted[0];
  const shop = { slots: [
    { kind: 'weapon_mod', definition: mod },
    { kind: 'existing_offer', id: 'offer_b' },
    { kind: 'existing_offer', id: 'offer_c' },
  ] };
  assert.equal(shop.slots.length, 3, 'AI mod occupies one of the existing shop slots');
  const preview = `${mod.name}：${mod.tradeoff}`;
  assert.match(preview, /放弃宽幅横扫/);
  assert.equal(shop.slots[0].kind, 'weapon_mod', 'player selects the offered card explicitly');
  assert.equal(buyModification(run, shop.slots[0].definition), true);
  const result = resolveAttackForm(run.weapons[0]);
  assert.equal(result.label, 'forward_thrust');
  assert.equal(result.coverage, 'narrow_line');
  assert.equal(run.materials, 6);
  assert.equal(run.weapons[0].baseForm, 'Swing', 'shared/base weapon definition remains unchanged');

  const illegal = validateProposal(run, { candidateIds: ['invented_script'] });
  assert.equal(illegal.ok, false);

  return { intent, preview, result, remainingMaterials: run.materials, invalidProposalRejected: !illegal.ok };
}

const result = runClosedLoop();
console.log('AI build-forge MVP loop passed (fixture provider, no live model):');
console.log(JSON.stringify(result, null, 2));
