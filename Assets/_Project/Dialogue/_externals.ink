// 게임(C#)이 제공하는 함수. 모든 대본은 이 파일을 INCLUDE 한다.
// Inky에서 미리보기할 때는 아래의 대체 함수가 대신 실행된다.
//
// 값 키 규칙: "정파" "사파" "마교" / "평판.<문파>" / "호감.<인물>" / 그 외 플래그
// 전투는 줄 끝 태그로 요청한다:  # battle: <전투 id>
// 상점도 태그로 연다:  # shop: <상점 id>
// 수련터의 일주천:  # circulate: <스탯 Con|Outer|Light>

EXTERNAL get(key)
EXTERNAL add(key, amount)
EXTERNAL set(key, value)
EXTERNAL money()
EXTERNAL pay(amount)
EXTERNAL earn(amount)
EXTERNAL give(item, amount)
EXTERNAL recruit(name)
EXTERNAL party_full()
EXTERNAL rest_inn()
EXTERNAL inner_power(amount)
EXTERNAL quest(id, stage, note)
EXTERNAL quest_done(id, note)
EXTERNAL quest_stage(id)

// 산중 수련
EXTERNAL enter_mountain()
EXTERNAL spend(fatigue)
EXTERNAL can_train()
EXTERNAL eat()
EXTERNAL make_fire()
EXTERNAL sleep_mountain()
EXTERNAL find_art(file)

// 문파. join_blocker는 입문을 막는 이유("" "소속" "파문" "성향" "평판")를 돌려준다.
EXTERNAL sect()
EXTERNAL join_blocker(id)
EXTERNAL join(id)
EXTERNAL is_disciple()
EXTERNAL disciple()
EXTERNAL knows(art)
EXTERNAL learn(art)
EXTERNAL secret_job(risk)
EXTERNAL expel()

=== function get(key)
~ return 0

=== function add(key, amount)
~ return

=== function set(key, value)
~ return

=== function money()
~ return 100

=== function pay(amount)
~ return true

=== function earn(amount)
~ return

=== function give(item, amount)
~ return

=== function recruit(name)
~ return true

=== function party_full()
~ return false

=== function enter_mountain()
~ return

=== function spend(fatigue)
~ return true

=== function can_train()
~ return true

=== function eat()
~ return

=== function make_fire()
~ return

=== function sleep_mountain()
~ return

=== function find_art(file)
~ return

=== function rest_inn()
~ return true

=== function inner_power(amount)
~ return

=== function quest(id, stage, note)
~ return

=== function quest_done(id, note)
~ return

=== function quest_stage(id)
~ return 0

=== function sect()
~ return ""

=== function join_blocker(id)
~ return ""

=== function join(id)
~ return true

=== function is_disciple()
~ return false

=== function disciple()
~ return true

=== function knows(art)
~ return false

=== function learn(art)
~ return true

=== function secret_job(risk)
~ return false

=== function expel()
~ return
