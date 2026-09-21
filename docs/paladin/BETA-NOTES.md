# Paladin beta release notes draft

Paladin: Oath of the Dawn adds a Vitality-based protector class, six equipment
sets and three alternative endgame specializations. One marketplace mod enables
the class, its two Censure actions and all 36 equipment items.

The class retains standard FTK bodies, faces, hair and race unlocks. The package
ships 135 original equipment and icon assets. Novice, Oathkeeper and Highward
provide progression; Mercy, Censure and Verdict offer healing, debuff protection
and retaliation alternatives. Other classes can equip the gear, while Guardian
bonuses require the Paladin class.

Guard is a guaranteed action targeting another living ally. It reduces direct
attack damage by 50 percent until the Paladin's next turn or incapacitation.
Focused hits heal the designated ally. Divine Intervention prevents one lethal
Guarded hit per Paladin per combat, leaving the ally at one HP. Revival does not
refresh a spent rescue charge.

## Requirements and availability

This draft targets package 0.1.0 and framework 0.1.4. Both remain unpublished;
these notes are not an installation announcement. The package cannot run on the
previous public framework because it requires the new Guardian and equipment
capabilities. Final download links belong here only after the matching framework
and package artifacts have been published and verified.

## Validation and limits

Local macOS evidence covers class creation, starting equipment, selected shop
purchases, controlled loot collection, Guard and rescue, Censure application/expiry,
representative Ward and Verdict effects, and enhanced Mercy healing. Native death,
revival and Guard recast preserve spent rescue. The user manually completed the
current art candidate's native Save and Exit flow and confirmed a fresh resume
with Paladin starter equipment retained. This user-operated evidence has no
automation receipt. Fixture-assisted outcomes and ordinary gameplay are
distinguished in the [acceptance record](VALIDATION.md).

Online co-op is explicitly unverified. Production promotion waits for community
host/client and multiple-Paladin confirmation. Linux, Windows and Proton gameplay
are also unverified. The framework's macOS launcher is not notarized; Gatekeeper
may require right-click > Open, and Steam artwork may need a Steam restart.

Balance and unobserved appearance/motion combinations remain beta limitations.
Representative Mercy equipment motion has been observed on native male and female
bodies; untested set/race combinations remain outside that evidence.
[Same-session ordinary combat recharge](next-combat-live.json) passed after a
spent rescue and native victory. [Dungeon reused-dummy recharge](dungeon-recharge-live.json)
and [body/foot resource retirement](apparel-retirement-live.json) also passed their
bounded live trials. [Native stun application/removal](incapacity-live.json) expires
Guard and leaves it expired after recovery. Enemy-applied stun, natural timed
recovery, petrification, separate rigid-instance retirement,
final equipment-motion acceptance and marketplace download validation remain
delivery work, not completed results. Do not describe this draft as a released beta.

## Maintainer publication candidate

The [local publication-candidate receipt](publication-candidate.json) pins the
reviewable descriptor, catalog candidate and unchanged package archive. The
proposed package tag is `paladin-v0.1.0-beta`, separate from framework `v0.1.4`.
Both remain unpublished. The current unpublished local archive is
`paladin-local-beta-0.1.0-06a6356760be.zip`, SHA-256
`06a6356760be8b167948751013efa497ebdeb3a9e5bfd33417f68b35f329c3db`. The candidate advertises only the verified macOS game
fingerprint and retains explicit online and other-platform limitations.

After remaining local gates and source review, commit the reviewed source and
build framework artifacts through the clean-tree release procedure. Publish the
framework preview and immutable package assets, then download and verify the
package bytes before adding the descriptor to the production catalog. Finally
exercise the actual Discover/install/enable route against that published catalog.
Local descriptor validation does not satisfy those download and client gates.
