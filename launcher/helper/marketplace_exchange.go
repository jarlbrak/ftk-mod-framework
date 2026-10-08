package main

import (
	"errors"
)

type marketThiefArmor struct {
	Family string `json:"family"`
	Slot   string `json:"slot"`
}

func marketThiefFamily(family string) bool {
	return contains([]string{"locksmith", "nightblade", "wayfarer"}, family)
}

func marketExchangeEntry(kind string, armor *marketThiefArmor, armament *string,
	exchange *marketTownExchange) error {
	if armor != nil && (kind != "item" || !marketThiefFamily(armor.Family) ||
		!contains([]string{"head", "body", "feet"}, armor.Slot)) {
		return errors.New("invalid thiefArmor declaration")
	}
	if armament != nil && (kind != "weapon" || !marketThiefFamily(*armament)) {
		return errors.New("invalid thiefArmament declaration")
	}
	// Shared drop and offer rules are validated by marketProgression.
	// Legacy item-owned catalogs remain admitted; class catalogs use one framework token.
	if exchange != nil && kind == "class" && exchange.Token != "" &&
		exchange.Token != "equipment_token" &&
		exchange.Token != "com.ftkmf.equipment-exchange:equipment_token" {
		return errors.New("townExchange requires the framework Guild Token")
	}
	return nil
}
