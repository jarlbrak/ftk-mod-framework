package main

import (
	"errors"
	"strings"
)

type marketGuardianProfile struct {
	PhysicalPercent       *int                   `json:"physicalPercent"`
	SmitePercent          *int                   `json:"smitePercent"`
	HealingPercent        *int                   `json:"healingPercent"`
	GuardReductionPercent *int                   `json:"guardReductionPercent"`
	Bonuses               *marketGuardianBonuses `json:"bonuses,omitempty"`
}

type marketGuardianSet struct {
	ID                string                       `json:"id"`
	Head              string                       `json:"head"`
	Body              string                       `json:"body"`
	Feet              string                       `json:"feet"`
	OneHand           string                       `json:"oneHand"`
	Shield            string                       `json:"shield"`
	TwoHand           string                       `json:"twoHand"`
	Minor             *marketGuardianProfile       `json:"minor"`
	Core              *marketGuardianProfile       `json:"core"`
	Completion        *marketGuardianBonuses       `json:"completion"`
	CoreProficiencies []string                     `json:"coreProficiencies,omitempty"`
	ArmorDamageBonus  *marketResistanceDamageBonus `json:"armorDamageBonus,omitempty"`
}

type marketEnemyDropRule struct {
	MinimumDisplayedLevel   *int       `json:"minimumDisplayedLevel"`
	OrdinaryChancePercent   *int       `json:"ordinaryChancePercent"`
	BossChancePercent       *int       `json:"bossChancePercent"`
	GuaranteedByOpportunity *int       `json:"guaranteedByOpportunity"`
	NamedBossGroups         [][]string `json:"namedBossGroups"`
}

type marketTownExchange struct {
	Token  string                    `json:"token,omitempty"`
	Offers []marketTownExchangeOffer `json:"offers"`
}

type marketTownExchangeCatalog struct {
	OwnerClass string                    `json:"ownerClass"`
	Offers     []marketTownExchangeOffer `json:"offers"`
}

type marketTownExchangeOffer struct {
	Item   string `json:"item"`
	Name   string `json:"name"`
	Family string `json:"family"`
	Slot   string `json:"slot"`
}

func marketProgressionBonuses(b *marketGuardianBonuses) bool {
	return b == nil || b.GuardHealPercent >= 0 && b.GuardHealPercent <= 20 &&
		b.FocusHealBonusPercent >= 0 && b.FocusHealBonusPercent <= 20 &&
		b.RetaliationDamage >= 0 && b.RetaliationDamage <= 20 && b.GuardFocusRestore >= 0 && b.GuardFocusRestore <= 1
}

func marketProfile(p *marketGuardianProfile) bool {
	if p == nil || p.PhysicalPercent == nil || p.SmitePercent == nil || p.HealingPercent == nil || p.GuardReductionPercent == nil {
		return false
	}
	return *p.PhysicalPercent >= 25 && *p.PhysicalPercent <= 200 && *p.SmitePercent >= 25 && *p.SmitePercent <= 200 &&
		*p.HealingPercent >= 25 && *p.HealingPercent <= 200 && *p.GuardReductionPercent >= 0 && *p.GuardReductionPercent <= 50 &&
		marketProgressionBonuses(p.Bonuses)
}

func marketProgression(kind string, guardian bool, profile *marketGuardianProfile, smite string,
	sets []marketGuardianSet, drops *marketEnemyDropRule, exchange *marketTownExchange) error {
	if profile != nil || smite != "" || sets != nil {
		if kind != "class" || !guardian || profile != nil && !marketProfile(profile) || smite != "" && strings.TrimSpace(smite) == "" {
			return errors.New("invalid Guardian profile declaration")
		}
		if sets != nil && (len(sets) == 0 || len(sets) > 16) {
			return errors.New("invalid Guardian set count")
		}
		seen := map[string]bool{}
		armor := map[string]bool{}
		for _, set := range sets {
			if strings.TrimSpace(set.ID) == "" || seen[set.ID] || !marketProfile(set.Minor) || !marketProfile(set.Core) || !marketProgressionBonuses(set.Completion) ||
				!marketActionReferences([]string{set.Head, set.Body, set.Feet, set.OneHand, set.Shield, set.TwoHand}, 6, 6) ||
				set.CoreProficiencies != nil && !marketActionReferences(set.CoreProficiencies, 0, 8) {
				return errors.New("invalid Guardian set members or profiles")
			}
			seen[set.ID] = true
			if b := set.ArmorDamageBonus; b != nil {
				if !marketActionReferences(b.Sources, 1, 16) || b.Multiplier <= 1 || b.Multiplier > 2 {
					return errors.New("invalid set armor damage bonus")
				}
			}
			for _, id := range []string{set.Head, set.Body, set.Feet} {
				if armor[id] {
					return errors.New("Guardian sets cannot share armor members")
				}
				armor[id] = true
			}
		}
	}
	if drops != nil {
		if kind != "item" || drops.MinimumDisplayedLevel == nil || drops.OrdinaryChancePercent == nil || drops.BossChancePercent == nil || drops.GuaranteedByOpportunity == nil ||
			*drops.MinimumDisplayedLevel < 0 || *drops.MinimumDisplayedLevel > 100 ||
			*drops.OrdinaryChancePercent < 0 || *drops.OrdinaryChancePercent > 100 ||
			*drops.BossChancePercent < 0 || *drops.BossChancePercent > 100 ||
			*drops.GuaranteedByOpportunity < 1 || *drops.GuaranteedByOpportunity > 1000 {
			return errors.New("invalid enemy drop rule")
		}
		seen := map[string]bool{}
		for _, group := range drops.NamedBossGroups {
			if !marketActionReferences(group, 1, 128) {
				return errors.New("invalid named boss group")
			}
			for _, id := range group {
				if seen[id] {
					return errors.New("named boss appears in multiple groups")
				}
				seen[id] = true
			}
		}
	}
	if exchange != nil {
		if kind != "item" && kind != "class" || len(exchange.Offers) == 0 || len(exchange.Offers) > 128 {
			return errors.New("invalid town exchange catalog")
		}
		seen := map[string]bool{}
		for _, offer := range exchange.Offers {
			if strings.TrimSpace(offer.Item) == "" || seen[offer.Item] || strings.TrimSpace(offer.Name) == "" || strings.TrimSpace(offer.Family) == "" ||
				!contains([]string{"Head", "Body", "Foot", "RightHand", "LeftHand", "Trinket", "Neck"}, offer.Slot) {
				return errors.New("invalid town exchange offer")
			}
			seen[offer.Item] = true
		}
	}
	return nil
}

func marketExchangeCatalogs(catalogs []marketTownExchangeCatalog) error {
	if catalogs == nil {
		return nil
	}
	if len(catalogs) == 0 || len(catalogs) > 128 {
		return errors.New("invalid town exchange catalog count")
	}
	for _, catalog := range catalogs {
		if strings.TrimSpace(catalog.OwnerClass) == "" {
			return errors.New("town exchange ownerClass is required")
		}
		if err := marketProgression("class", false, nil, "", nil, nil,
			&marketTownExchange{Offers: catalog.Offers}); err != nil {
			return err
		}
	}
	return nil
}
