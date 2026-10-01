package main

import (
	"fmt"
	"strings"
	"testing"
)

func marketTestIDs(prefix string, count int) string {
	ids := make([]string, count)
	for i := range ids {
		ids[i] = fmt.Sprintf(`"%s%d"`, prefix, i)
	}
	return strings.Join(ids, ",")
}

func TestMarketWeaponProficiencies(t *testing.T) {
	for _, test := range []struct {
		name, kind, declaration string
		valid                   bool
	}{
		{"class mapping", "class", `"weaponProficiencies":[{"weapons":["hammerOne","hammerTwo"],"proficiencies":["censure","smite"]}]`, true},
		{"two groups", "class", `"weaponProficiencies":[{"weapons":["hammerOne"],"proficiencies":["censure"]},{"weapons":["hammerTwo"],"proficiencies":["smite"]}]`, true},
		{"wrong kind", "weapon", `"weaponProficiencies":[{"weapons":["hammerOne"],"proficiencies":["smite"]}]`, false},
		{"null groups", "class", `"weaponProficiencies":null`, false},
		{"empty groups", "class", `"weaponProficiencies":[]`, false},
		{"wrong groups type", "class", `"weaponProficiencies":{}`, false},
		{"null group", "class", `"weaponProficiencies":[null]`, false},
		{"empty weapons", "class", `"weaponProficiencies":[{"weapons":[],"proficiencies":["smite"]}]`, false},
		{"missing weapons", "class", `"weaponProficiencies":[{"proficiencies":["smite"]}]`, false},
		{"blank weapon", "class", `"weaponProficiencies":[{"weapons":[" "],"proficiencies":["smite"]}]`, false},
		{"duplicate weapon", "class", `"weaponProficiencies":[{"weapons":["hammer","hammer"],"proficiencies":["smite"]}]`, false},
		{"empty actions", "class", `"weaponProficiencies":[{"weapons":["hammer"],"proficiencies":[]}]`, false},
		{"missing actions", "class", `"weaponProficiencies":[{"weapons":["hammer"]}]`, false},
		{"blank action", "class", `"weaponProficiencies":[{"weapons":["hammer"],"proficiencies":[" "]}]`, false},
		{"duplicate action", "class", `"weaponProficiencies":[{"weapons":["hammer"],"proficiencies":["smite","smite"]}]`, false},
		{"unknown member", "class", `"weaponProficiencies":[{"weapons":["hammer"],"proficiencies":["smite"],"other":true}]`, false},
		{"wrong weapon type", "class", `"weaponProficiencies":[{"weapons":[1],"proficiencies":["smite"]}]`, false},
		{"wrong action type", "class", `"weaponProficiencies":[{"weapons":["hammer"],"proficiencies":[1]}]`, false},
	} {
		t.Run(test.name, func(t *testing.T) {
			body := `{"entries":[{"kind":"` + test.kind + `","id":"test","template":"native",` + test.declaration + `}]}`
			if err := marketContent([]byte(body)); (err == nil) != test.valid {
				t.Fatalf("valid=%v, err=%v", test.valid, err)
			}
		})
	}
}

func TestMarketWeaponProficiencyBounds(t *testing.T) {
	for _, test := range []struct {
		name, groups string
		valid        bool
	}{
		{"max groups", strings.TrimSuffix(strings.Repeat(`{"weapons":["hammer"],"proficiencies":["smite"]},`, 16), ","), true},
		{"too many groups", strings.TrimSuffix(strings.Repeat(`{"weapons":["hammer"],"proficiencies":["smite"]},`, 17), ","), false},
		{"max weapons", `{"weapons":[` + marketTestIDs("hammer", 128) + `],"proficiencies":["smite"]}`, true},
		{"too many weapons", `{"weapons":[` + marketTestIDs("hammer", 129) + `],"proficiencies":["smite"]}`, false},
		{"max actions", `{"weapons":["hammer"],"proficiencies":[` + marketTestIDs("action", 16) + `]}`, true},
		{"too many actions", `{"weapons":["hammer"],"proficiencies":[` + marketTestIDs("action", 17) + `]}`, false},
	} {
		t.Run(test.name, func(t *testing.T) {
			body := `{"entries":[{"kind":"class","id":"test","template":"native","weaponProficiencies":[` + test.groups + `]}]}`
			if err := marketContent([]byte(body)); (err == nil) != test.valid {
				t.Fatalf("valid=%v, err=%v", test.valid, err)
			}
		})
	}
}

func TestMarketExplicitEmptyWeaponProficiencies(t *testing.T) {
	for _, test := range []struct {
		name, kind, declaration string
		valid                   bool
	}{
		{"explicit clear", "weapon", `"replaceProficiencies":true,"proficiencies":[]`, true},
		{"replacement with action", "weapon", `"replaceProficiencies":true,"proficiencies":["strike"]`, true},
		{"missing actions", "weapon", `"replaceProficiencies":true`, false},
		{"null actions", "weapon", `"replaceProficiencies":true,"proficiencies":null`, false},
		{"wrong kind", "item", `"replaceProficiencies":true,"proficiencies":[]`, false},
		{"class grants stay nonempty", "class", `"proficiencies":[]`, false},
		{"item grants stay nonempty", "item", `"proficiencies":[]`, false},
	} {
		t.Run(test.name, func(t *testing.T) {
			body := `{"entries":[{"kind":"` + test.kind + `","id":"test","template":"native",` + test.declaration + `}]}`
			if err := marketContent([]byte(body)); (err == nil) != test.valid {
				t.Fatalf("valid=%v, err=%v", test.valid, err)
			}
		})
	}
}

func TestMarketNativeWeaponAttackFields(t *testing.T) {
	for _, test := range []struct {
		name, kind, fields string
		valid              bool
	}{
		{"native strike", "weapon", `"fields":{"m_AttackDisplay":"STRIKE","m_NoRegularAttack":false}`, true},
		{"attack display type", "weapon", `"fields":{"m_AttackDisplay":42}`, false},
		{"regular attack type", "weapon", `"fields":{"m_NoRegularAttack":"false"}`, false},
		{"attack display kind", "item", `"fields":{"m_AttackDisplay":"STRIKE"}`, false},
		{"regular attack kind", "item", `"fields":{"m_NoRegularAttack":false}`, false},
	} {
		t.Run(test.name, func(t *testing.T) {
			body := `{"entries":[{"kind":"` + test.kind + `","id":"test","template":"native",` + test.fields + `}]}`
			if err := marketContent([]byte(body)); (err == nil) != test.valid {
				t.Fatalf("valid=%v, err=%v", test.valid, err)
			}
		})
	}
}
